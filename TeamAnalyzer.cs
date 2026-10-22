using System;
using System.Collections.Generic;

namespace SanaBuildOverlay
{
    public sealed class TeamMemberSnapshot
    {
        public string UserName = "Неизвестно";
        public string CharacterName = "Тело недоступно";
        public bool IsLocal, BodyVisible, InventoryVisible, Alive;
        public int TotalStacks;
        public BuildSnapshot Build;
        public BuildMetrics Metrics;
        public RunReadiness Readiness;
        public AdvancedAssessment Advanced;
        public ItemAdvice BestAdvice;
        public int OverallReadiness = -1;
        public int WeakestPercent = -1;
        public string WeakestArea = "нет данных";
        public string Role = "Нет данных";
    }

    public sealed class BuildMetrics
    {
        public int Offense, Defense, Mobility, Synergy;
        public bool CoreReady;
    }

    public static class BuildProfileAnalyzer
    {
        public static BuildMetrics Measure(BuildSnapshot build)
        {
            var metrics = new BuildMetrics();
            if (build == null || !build.InventoryKnown) return metrics;
            double offense = 0d, defense = 0d, mobility = 0d, synergy = 0d;
            foreach (ItemRule rule in ItemDatabase.Rules)
            {
                int count = build.Count(rule.Id);
                if (count <= 0) continue;
                // Distinct components matter most; repeated stacks still count,
                // but are capped so one huge stack cannot masquerade as a complete build.
                double value = 14d + Math.Min(9, count - 1) * 4d;
                if (rule.Category == "offense") offense += value;
                else if (rule.Category == "defense" || rule.Category == "healing") defense += value;
                else if (rule.Category == "movement") mobility += value;
                else synergy += value;
            }
            // Live ItemDef tags cover installed DLC/mod items that do not yet have
            // an exact formula. They add broad coverage only and never claim DPS.
            offense += Math.Min(55d, build.LiveDamageItems * 5d);
            defense += Math.Min(38d, build.LiveHealingItems * 5d);
            mobility += Math.Min(24d, build.LiveUtilityItems * 2d);
            synergy += Math.Min(35d, build.LiveAoeItems * 7d);
            metrics.Offense = Percent(offense, 110d);
            metrics.Defense = Percent(defense, 75d);
            metrics.Mobility = Percent(mobility, 45d);
            metrics.Synergy = Percent(synergy, 65d);
            metrics.CoreReady = metrics.Defense >= 30 && metrics.Mobility >= 30;
            return metrics;
        }

        public static bool IsBurstOpener(BuildSnapshot build)
        {
            if (build == null) return false;
            string token = build.CharacterToken;
            return Has(token, "RAILGUNNER") || Has(token, "LOADER") || Has(token, "BANDIT") || Has(token, "MAGE")
                || Has(token, "TOOLBOT") && build.Primary != null && build.Primary.Id == "TOOLBOT_PRIMARY_ALT1_NAME";
        }

        public static bool IsRapidProc(BuildSnapshot build)
        {
            if (build == null) return false;
            string token = build.CharacterToken;
            bool nailgun = Has(token, "TOOLBOT") && (build.Primary == null || build.Primary.Id == "TOOLBOT_PRIMARY_NAME");
            return Has(token, "COMMANDO") || Has(token, "HUNTRESS") || nailgun || Has(token, "CAPTAIN");
        }

        public static bool IsCloseRange(BuildSnapshot build)
        {
            string token = build == null ? "" : build.CharacterToken;
            return Has(token, "MERC") || Has(token, "LOADER") || Has(token, "FALSESON") || Has(token, "CROCO") || Has(token, "ENFORCER");
        }

        private static bool Has(string token, string part)
        { return !string.IsNullOrEmpty(token) && token.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0; }

        private static int Percent(double value, double target)
        { return Math.Max(0, Math.Min(100, (int)Math.Round(value * 100d / target))); }
    }

    public sealed class TeamDistribution
    {
        public string ItemId, UserName, CharacterName, Reason;
        public string TargetArea;
        public int TargetPercent = -1, OverallPercent = -1;
        public double Score;
    }

    public static class TeamAnalyzer
    {
        public static TeamDistribution ChooseRecipient(string itemId, IList<TeamMemberSnapshot> members)
        {
            if (string.IsNullOrEmpty(itemId) || members == null) return null;
            string category = ItemCategory(itemId);
            TeamDistribution best = null;
            foreach (TeamMemberSnapshot member in members)
            {
                if (member == null || !member.InventoryVisible || member.Build == null) continue;
                List<ItemAdvice> advice = RecommendationEngine.Evaluate(member.Build);
                ItemAdvice match = advice.Find(delegate(ItemAdvice a) { return a.Id == itemId; });
                if (match == null) continue;
                int target = TargetReadiness(member, category, itemId);
                int overall = member.OverallReadiness >= 0 ? member.OverallReadiness : Overall(member.Readiness);
                double needBonus = target < 0 ? 0d : Math.Max(0, 100 - target) * 0.75d;
                double teamBonus = overall < 0 ? 0d : Math.Max(0, 100 - overall) * 0.15d;
                var candidate = new TeamDistribution { ItemId = itemId, UserName = member.UserName,
                    CharacterName = member.CharacterName, Reason = match.Reason, Score = match.Score + needBonus + teamBonus,
                    TargetArea = AreaName(category, itemId), TargetPercent = target, OverallPercent = overall };
                if (best == null || candidate.Score > best.Score ||
                    (candidate.Score == best.Score && string.CompareOrdinal(candidate.UserName, best.UserName) < 0)) best = candidate;
            }
            return best;
        }

        public static void Summarize(TeamMemberSnapshot member)
        {
            if (member == null) return;
            member.OverallReadiness = Overall(member.Readiness);
            member.WeakestPercent = -1;
            member.WeakestArea = "нет данных";
            if (member.Readiness == null) return;
            ConsiderWeakest(member, "урон", member.Readiness.Damage);
            ConsiderWeakest(member, "HP", member.Readiness.Health);
            ConsiderWeakest(member, "защита", member.Readiness.Defense);
            ConsiderWeakest(member, "движение", member.Readiness.Mobility);
            if (member.Advanced != null)
            {
                ConsiderWeakest(member, "первый удар", member.Advanced.Opener);
                ConsiderWeakest(member, "темп урона", member.Advanced.Tempo);
                ConsiderWeakest(member, "сильный удар", member.Advanced.Burst);
                ConsiderWeakest(member, "затяжной бой", member.Advanced.Sustain);
                ConsiderWeakest(member, "зачистка", member.Advanced.Clear);
                int advanced = OverallAdvanced(member.Advanced);
                if (advanced >= 0 && member.OverallReadiness >= 0)
                    member.OverallReadiness = (int)Math.Round(member.OverallReadiness * 0.7d + advanced * 0.3d);
                else if (advanced >= 0) member.OverallReadiness = advanced;
            }
        }

        public static TeamMemberSnapshot FindWeakest(IList<TeamMemberSnapshot> members)
        {
            TeamMemberSnapshot weakest = null;
            if (members == null) return null;
            foreach (TeamMemberSnapshot member in members)
            {
                if (member == null || !member.Alive || member.OverallReadiness < 0) continue;
                if (weakest == null || member.OverallReadiness < weakest.OverallReadiness ||
                    (member.OverallReadiness == weakest.OverallReadiness && string.CompareOrdinal(member.UserName, weakest.UserName) < 0))
                    weakest = member;
            }
            return weakest;
        }

        private static int Overall(RunReadiness readiness)
        {
            if (readiness == null) return -1;
            int[] values = { readiness.Damage.Percent, readiness.Health.Percent, readiness.Defense.Percent, readiness.Mobility.Percent };
            int count = 0, sum = 0, minimum = int.MaxValue;
            foreach (int value in values)
            {
                if (value < 0) continue;
                count++; sum += value; minimum = Math.Min(minimum, value);
            }
            if (count == 0) return -1;
            double average = (double)sum / count;
            return Math.Max(0, Math.Min(150, (int)Math.Round(average * 0.60d + minimum * 0.40d)));
        }

        private static void ConsiderWeakest(TeamMemberSnapshot member, string area, ReadinessMetric metric)
        {
            if (metric == null || metric.Percent < 0) return;
            if (member.WeakestPercent < 0 || metric.Percent < member.WeakestPercent)
            { member.WeakestPercent = metric.Percent; member.WeakestArea = area; }
        }

        private static int OverallAdvanced(AdvancedAssessment assessment)
        {
            if (assessment == null) return -1;
            int[] values = { assessment.Opener.Percent, assessment.Tempo.Percent, assessment.Burst.Percent,
                assessment.Sustain.Percent, assessment.Clear.Percent };
            int count = 0, sum = 0, minimum = int.MaxValue;
            foreach (int value in values)
            {
                if (value < 0) continue;
                count++; sum += value; minimum = Math.Min(minimum, value);
            }
            return count == 0 ? -1 : (int)Math.Round(((double)sum / count) * 0.55d + minimum * 0.45d);
        }

        private static int TargetReadiness(TeamMemberSnapshot member, string category, string itemId)
        {
            if (member == null || member.Readiness == null) return -1;
            if (category == "offense")
            {
                int damage = member.Readiness.Damage.Percent;
                int clear = member.Advanced == null ? -1 : member.Advanced.Clear.Percent;
                if (!IsClearItem(itemId) || clear < 0) return damage;
                if (damage < 0) return clear;
                return Math.Min(damage, clear);
            }
            if (category == "movement") return member.Readiness.Mobility.Percent;
            if (category == "defense" || category == "healing")
            {
                int hp = member.Readiness.Health.Percent, defense = member.Readiness.Defense.Percent;
                if (hp < 0) return defense;
                if (defense < 0) return hp;
                return Math.Min(hp, defense);
            }
            return member.OverallReadiness >= 0 ? member.OverallReadiness : Overall(member.Readiness);
        }

        private static string ItemCategory(string itemId)
        {
            foreach (ItemRule rule in ItemDatabase.Rules)
                if (string.Equals(rule.Id, itemId, StringComparison.OrdinalIgnoreCase)) return rule.Category;
            return "utility";
        }

        private static bool IsClearItem(string itemId)
        {
            return itemId == "ChainLightning" || itemId == "ExplodeOnDeath" || itemId == "ExplodeOnDeathVoid"
                || itemId == "Behemoth" || itemId == "Dagger" || itemId == "Missile" || itemId == "MissileVoid";
        }

        private static string AreaName(string category, string itemId)
        {
            if (category == "offense" && IsClearItem(itemId)) return "урон/зачистка";
            if (category == "offense") return "урон";
            if (category == "movement") return "движение";
            if (category == "defense" || category == "healing") return "живучесть";
            return "общая готовность";
        }
    }
}
