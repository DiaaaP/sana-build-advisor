using System;
using System.Collections.Generic;
using System.Globalization;

namespace SanaBuildOverlay
{
    public sealed class WhatIfImpact
    {
        public string ItemId = "";
        public int Opener, Tempo, Burst, Sustain, Clear, Damage, Health, Defense, Mobility;
        public int BestDelta;
        public string Summary = "Изменение поддержанных полос не обнаружено.";
    }

    public sealed class DecisionReport
    {
        public string BuildDna = "НЕ ОПРЕДЕЛЁН";
        public string Synergy = "Сильная незавершённая связка не найдена.";
        public string Overinvestment = "Явной перекачки одной категории нет.";
        public string Danger = "Нет данных об угрозе.";
        public string Teleporter = "Готовность телепортера не рассчитана.";
        public string TeamRole = "Универсал";
        public string LootFit = "—";
        public string Scrap = "Нет безопасного кандидата для утилизации.";
        public string Notification = "";
    }

    public static class DecisionAdvisor
    {
        public static DecisionReport Analyze(BuildSnapshot b, BuildMetrics metrics, RunReadiness readiness,
            AdvancedAssessment advanced, IList<ItemAdvice> ranked)
        {
            var r = new DecisionReport();
            if (b == null) return r;
            metrics = metrics ?? new BuildMetrics();
            advanced = advanced ?? new AdvancedAssessment();
            r.BuildDna = BuildDna(b, metrics, advanced);
            r.TeamRole = TeamRole(b, metrics, advanced);
            r.LootFit = LootFit(readiness, advanced);
            r.Synergy = Synergy(b);
            r.Overinvestment = Overinvestment(b, readiness, advanced);
            r.Danger = advanced.Priority;
            r.Teleporter = Teleporter(readiness, advanced);
            r.Scrap = ScrapAdvice(b, ranked);
            r.Notification = Notification(readiness, advanced, r.Overinvestment);
            return r;
        }

        public static WhatIfImpact Simulate(BuildSnapshot original, string itemId, RunReadiness readiness,
            AdvancedAssessment before)
        {
            var impact = new WhatIfImpact { ItemId = itemId };
            if (original == null || string.IsNullOrEmpty(itemId)) return impact;
            BuildSnapshot afterBuild = Clone(original);
            afterBuild.Counts[itemId] = afterBuild.Count(itemId) + 1;
            ApplyKnownStat(afterBuild, itemId);
            BuildMetrics afterMetrics = BuildProfileAnalyzer.Measure(afterBuild);
            RunReadiness afterReadiness = AdjustReadiness(readiness, itemId);
            AdvancedAssessment after = AdvancedAdvisor.Evaluate(afterBuild, afterMetrics, afterReadiness);
            before = before ?? AdvancedAdvisor.Evaluate(original, BuildProfileAnalyzer.Measure(original), readiness);
            impact.Opener = Delta(before.Opener, after.Opener);
            impact.Tempo = Delta(before.Tempo, after.Tempo);
            impact.Burst = Delta(before.Burst, after.Burst);
            impact.Sustain = Delta(before.Sustain, after.Sustain);
            impact.Clear = Delta(before.Clear, after.Clear);
            if (readiness != null && afterReadiness != null)
            {
                impact.Damage = Delta(readiness.Damage, afterReadiness.Damage);
                impact.Health = Delta(readiness.Health, afterReadiness.Health);
                impact.Defense = Delta(readiness.Defense, afterReadiness.Defense);
                impact.Mobility = Delta(readiness.Mobility, afterReadiness.Mobility);
            }
            int[] values = { impact.Opener, impact.Tempo, impact.Burst, impact.Sustain, impact.Clear,
                impact.Damage, impact.Health, impact.Defense, impact.Mobility };
            foreach (int value in values) impact.BestDelta = Math.Max(impact.BestDelta, value);
            impact.Summary = Summary(impact);
            return impact;
        }

        public static string TeamRole(BuildSnapshot b, BuildMetrics m, AdvancedAssessment a)
        {
            if (b == null) return "Нет данных";
            if (BuildProfileAnalyzer.IsBurstOpener(b) && a != null && a.Opener.Percent >= 110) return "Убийца боссов";
            if (a != null && a.Clear.Percent >= 115) return "Зачистка толпы";
            if (BuildProfileAnalyzer.IsRapidProc(b)) return "Proc / постоянный урон";
            if (m != null && m.Defense >= 70 && m.Defense > m.Offense + 15) return "Танк / удержание зоны";
            if (m != null && m.Mobility >= 75) return "Мобильный разведчик";
            return "Универсал";
        }

        private static string BuildDna(BuildSnapshot b, BuildMetrics m, AdvancedAssessment a)
        {
            string core = BuildProfileAnalyzer.IsBurstOpener(b) ? "ВАНШОТ"
                : BuildProfileAnalyzer.IsRapidProc(b) ? "PROC" : BuildProfileAnalyzer.IsCloseRange(b) ? "БЛИЖНИЙ БОЙ" : "УНИВЕРСАЛ";
            if (a != null && a.Clear.Percent >= 120) core += " + ЗАЧИСТКА";
            if (m.Defense >= 75) core += " + ТАНК";
            if (b.HealthReadiness >= 0 && b.HealthReadiness < 75) core += " • СТЕКЛО";
            return core;
        }

        private static string Synergy(BuildSnapshot b)
        {
            if (BuildProfileAnalyzer.IsBurstOpener(b) && b.Count("Crowbar") > 0
                && b.Count("FireRing") + b.Count("IceRing") == 0)
                return "Почти готов burst-набор: Crowbar уже есть, не хватает Kjaro/Runald для тяжёлого попадания.";
            if (b.Count("Missile") + b.Count("MissileVoid") > 0 && b.Count("MoreMissile") == 0)
                return "Ракетная ветка активна: Pocket ICBM заметно завершит связку.";
            if (b.Count("BleedOnHit") > 0 && b.Count("BleedOnHitAndExplode") == 0)
                return "Bleed уже активен: Shatterspleen добавит взрыв после убийства кровоточащей цели.";
            if (b.CharacterToken.IndexOf("ENGI", StringComparison.OrdinalIgnoreCase) >= 0 && b.Count("Mushroom") == 0)
                return "Стационарным турелям Engineer не хватает Bustling Fungus.";
            if (b.LiveAoeItems == 0 && b.DamageReadiness >= 110)
                return "Урон по одной цели закрыт; один AoE/on-kill предмет завершит универсальную основу.";
            return "Ключевая связка уже работает либо данных для следующей части недостаточно.";
        }

        private static string Overinvestment(BuildSnapshot b, RunReadiness r, AdvancedAssessment a)
        {
            int survival = MinKnown(b.HealthReadiness, b.DefenseReadiness);
            if (b.DamageReadiness >= 135 && survival >= 0 && survival < 90)
                return "Урон перекачан, а живучесть просела: следующие атакующие предметы лучше уступать или менять на защиту.";
            StackGuidance crowbar = AdvancedAdvisor.Stack(b, "Crowbar", b.Count("Crowbar"));
            if (b.Count("Crowbar") > 0 && crowbar.State == "ПРЕДЕЛ")
                return "Crowbar достиг мягкого предела: новый стак слабее закрытия другой категории.";
            if (a != null && a.Opener.Percent >= 145 && a.Clear.Percent >= 0 && a.Clear.Percent < 75)
                return "Первый выстрел перекачан, а зачистка отстаёт.";
            return "Явной перекачки одной категории нет.";
        }

        private static string Teleporter(RunReadiness r, AdvancedAssessment a)
        {
            if (r == null || a == null) return "Недостаточно данных для телепортера.";
            int[] required = { r.Damage.Percent, r.Defense.Percent, r.Mobility.Percent,
                a.Burst.Percent, a.Sustain.Percent, a.Clear.Percent };
            int lowest = int.MaxValue;
            foreach (int value in required) if (value >= 0) lowest = Math.Min(lowest, value);
            if (lowest == int.MaxValue) return "Недостаточно данных для телепортера.";
            if (lowest < 70) return "Телепортер рискованный: одна из ключевых полос ниже 70%.";
            if (lowest < 95) return "Телепортер проходим, но есть заметная просадка. Проверь подсвеченную слабую полосу.";
            return "По поддержанным метрикам телепортер можно запускать.";
        }

        private static string ScrapAdvice(BuildSnapshot b, IList<ItemAdvice> ranked)
        {
            if (b == null || ranked == null) return "Нет безопасного кандидата для утилизации.";
            ItemAdvice worst = null;
            for (int i = 0; i < ranked.Count; i++)
            {
                ItemAdvice item = ranked[i];
                if (item == null || b.Count(item.Id) <= 0) continue;
                if ((item.Id == "Hoof" || item.Id == "Bear" || item.Id == "ArmorPlate") && b.Count(item.Id) <= 1) continue;
                if (worst == null || item.Score < worst.Score) worst = item;
            }
            return worst == null ? "Нет очевидно лишнего поддержанного предмета."
                : "Кандидат: " + worst.Id + " x" + b.Count(worst.Id) + " • текущий предельный балл "
                    + worst.Score.ToString("0", CultureInfo.CurrentCulture) + ". Не сдавай последний источник категории.";
        }

        private static string Notification(RunReadiness r, AdvancedAssessment a, string overinvestment)
        {
            if (a != null && a.Burst.Percent >= 0 && a.Burst.Percent < 65) return "ОПАСНО: слабая защита от сильного удара";
            if (a != null && a.Clear.Percent >= 0 && a.Clear.Percent < 40) return "НИЗКАЯ ЗАЧИСТКА: ищи AoE";
            if (!string.IsNullOrEmpty(overinvestment) && overinvestment.IndexOf("перекачан", StringComparison.OrdinalIgnoreCase) >= 0)
                return "СТОП СТАК: закрой слабую категорию";
            return "";
        }

        private static string LootFit(RunReadiness r, AdvancedAssessment a)
        {
            if (r == null || a == null) return "—";
            int[] values = { r.Damage.Percent, r.Health.Percent, r.Defense.Percent, r.Mobility.Percent,
                a.Opener.Percent, a.Tempo.Percent, a.Burst.Percent, a.Sustain.Percent, a.Clear.Percent };
            int sum = 0, count = 0, minimum = int.MaxValue;
            foreach (int value in values)
            {
                if (value < 0) continue;
                sum += Math.Min(120, value); count++; minimum = Math.Min(minimum, value);
            }
            if (count == 0) return "—";
            int fit = (int)Math.Round(((double)sum / count) * 0.7d + Math.Min(120, minimum) * 0.3d);
            return Math.Max(0, Math.Min(120, fit)).ToString(CultureInfo.CurrentCulture) + "%";
        }

        private static BuildSnapshot Clone(BuildSnapshot b)
        {
            var c = new BuildSnapshot { InventoryKnown = b.InventoryKnown, Railgunner = b.Railgunner,
                CharacterToken = b.CharacterToken, Crit = b.Crit, Damage = b.Damage, CritMultiplier = b.CritMultiplier,
                NaturalDamage = b.NaturalDamage, AttackSpeed = b.AttackSpeed, Health = b.Health,
                NaturalHealth = b.NaturalHealth, EffectiveHealth = b.EffectiveHealth, Armor = b.Armor,
                Regen = b.Regen, MoveSpeed = b.MoveSpeed, Stage = b.Stage, HostileCount = b.HostileCount,
                Difficulty = b.Difficulty, Encounter = b.Encounter, Style = b.Style, Goal = b.Goal,
                DamageReadiness = b.DamageReadiness, HealthReadiness = b.HealthReadiness,
                DefenseReadiness = b.DefenseReadiness, MobilityReadiness = b.MobilityReadiness,
                Primary = b.Primary, Secondary = b.Secondary, Utility = b.Utility, Special = b.Special,
                LiveDamageItems = b.LiveDamageItems, LiveHealingItems = b.LiveHealingItems,
                LiveUtilityItems = b.LiveUtilityItems, LiveAoeItems = b.LiveAoeItems };
            foreach (var pair in b.Counts) c.Counts[pair.Key] = pair.Value;
            foreach (string id in b.Available) c.Available.Add(id);
            foreach (var pair in b.Categories) c.Categories[pair.Key] = pair.Value;
            return c;
        }

        private static void ApplyKnownStat(BuildSnapshot b, string id)
        {
            if (id == "Syringe" && Valid(b.AttackSpeed)) b.AttackSpeed *= 1.15d;
            else if (id == "FlatHealth")
            {
                if (Valid(b.Health)) b.Health += 25d;
                if (Valid(b.EffectiveHealth)) b.EffectiveHealth += 25d / StatCalculator.ArmorDamageMultiplier(b.Armor);
            }
            else if (id == "PersonalShield" && Valid(b.Health)) b.Health += Valid(b.NaturalHealth) ? b.NaturalHealth * 0.08d : b.Health * 0.08d;
            string category;
            if (b.Categories.TryGetValue(id, out category))
            {
                if (category == "offense") b.LiveDamageItems++;
                else if (category == "healing") b.LiveHealingItems++;
                else if (category == "utility" || category == "movement") b.LiveUtilityItems++;
            }
            if (IsAoe(id)) b.LiveAoeItems++;
        }

        private static RunReadiness AdjustReadiness(RunReadiness original, string id)
        {
            if (original == null) return null;
            var r = new RunReadiness { Stage = original.Stage, Difficulty = original.Difficulty,
                Damage = Copy(original.Damage), Health = Copy(original.Health), Defense = Copy(original.Defense), Mobility = Copy(original.Mobility) };
            string category = Category(id);
            if (category == "offense") Add(r.Damage, 4);
            else if (category == "movement") Add(r.Mobility, id == "Hoof" ? 10 : 7);
            else if (category == "defense") { Add(r.Defense, 7); if (id == "FlatHealth" || id == "PersonalShield") Add(r.Health, 6); }
            else if (category == "healing") Add(r.Defense, 5);
            return r;
        }

        private static string Summary(WhatIfImpact i)
        {
            var parts = new List<string>();
            AddPart(parts, "1-й", i.Opener); AddPart(parts, "темп", i.Tempo); AddPart(parts, "удар", i.Burst);
            AddPart(parts, "бой", i.Sustain); AddPart(parts, "зач", i.Clear); AddPart(parts, "HP", i.Health);
            AddPart(parts, "защ", i.Defense); AddPart(parts, "движ", i.Mobility);
            return parts.Count == 0 ? "Поддержанные полосы почти не изменятся."
                : "Прогноз: " + string.Join(" • ", parts.ToArray()) + ".";
        }

        private static void AddPart(List<string> parts, string name, int value)
        { if (value != 0) parts.Add(name + " " + (value > 0 ? "+" : "") + value + "%"); }
        private static int Delta(ReadinessMetric before, ReadinessMetric after)
        { return before == null || after == null || before.Percent < 0 || after.Percent < 0 ? 0 : after.Percent - before.Percent; }
        private static void Add(ReadinessMetric metric, int value)
        { if (metric != null && metric.Percent >= 0) { metric.Percent = Math.Min(150, metric.Percent + value); metric.Status = Status(metric.Percent); } }
        private static ReadinessMetric Copy(ReadinessMetric m)
        { return m == null ? new ReadinessMetric() : new ReadinessMetric { Percent = m.Percent, Status = m.Status, Detail = m.Detail }; }
        private static string Status(int p) { return p < 70 ? "МАЛО" : p < 95 ? "ПРОСАДКА" : p < 120 ? "НОРМА" : "С ЗАПАСОМ"; }
        private static int MinKnown(int a, int b) { if (a < 0) return b; if (b < 0) return a; return Math.Min(a, b); }
        private static bool IsAoe(string id)
        { return id == "ChainLightning" || id == "ChainLightningVoid" || id == "ExplodeOnDeath" || id == "ExplodeOnDeathVoid"
            || id == "IgniteOnKill" || id == "Behemoth" || id == "Dagger" || id == "Missile" || id == "MissileVoid"
            || id == "BounceNearby" || id == "BleedOnHitAndExplode" || id == "MoreMissile"; }
        private static string Category(string id)
        { foreach (ItemRule rule in ItemDatabase.Rules) if (rule.Id == id) return rule.Category; return "utility"; }
        private static bool Valid(double v) { return !double.IsNaN(v) && !double.IsInfinity(v) && v > 0d; }
    }
}
