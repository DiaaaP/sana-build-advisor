using System;
using System.Globalization;

namespace SanaBuildOverlay
{
    public enum EncounterKind { Exploration, Swarm, Teleporter, Boss }

    public sealed class AdvancedAssessment
    {
        public ReadinessMetric Opener = new ReadinessMetric();
        public ReadinessMetric Tempo = new ReadinessMetric();
        public ReadinessMetric Burst = new ReadinessMetric();
        public ReadinessMetric Sustain = new ReadinessMetric();
        public ReadinessMetric Clear = new ReadinessMetric();
        public string Context = "Исследование";
        public string Priority = "Недостаточно данных";
    }

    public sealed class StackGuidance
    {
        public double MarginalPercent = double.NaN;
        public string State = "ОЦЕНКА";
        public string Text = "Точный предел для этого эффекта не рассчитан.";
    }

    public static class AdvancedAdvisor
    {
        public static AdvancedAssessment Evaluate(BuildSnapshot b, BuildMetrics coverage, RunReadiness baseReadiness)
        {
            var result = new AdvancedAssessment();
            if (b == null) return result;
            coverage = coverage ?? new BuildMetrics();
            result.Context = ContextName(b.Encounter, b.HostileCount);

            double pressure = Valid(b.Difficulty) ? Math.Log(Math.Max(1d, b.Difficulty), 2d) : 0d;
            int stage = Math.Max(1, b.Stage);
            double damageNeed = 1d + 0.20d * (stage - 1) + 0.10d * pressure;
            if (Valid(b.Damage) && Valid(b.NaturalDamage))
            {
                double baseRatio = b.Damage / b.NaturalDamage;
                double opener = baseRatio;
                if (BuildProfileAnalyzer.IsBurstOpener(b)) opener *= 1d + 0.75d * b.Count("Crowbar");
                if (b.Encounter == EncounterKind.Boss) opener *= 1d + 0.20d * b.Count("BossDamageBonus");
                result.Opener = Metric(opener, damageNeed,
                    "Прокси первого попадания: базовый урон персонажа и поддержанные условные множители. Коэффициент конкретного навыка и HP цели не известны.");
                double attackRate = Valid(b.AttackSpeed) ? b.AttackSpeed : 1d;
                double tempo = baseRatio * attackRate * (1d + Math.Min(0.65d, coverage.Offense / 100d * 0.45d));
                result.Tempo = Metric(tempo, damageNeed,
                    "Прокси темпа: урон × скорость атаки × ограниченное покрытие предметов. Откаты, proc coefficient и движение цели не симулируются.");
            }
            double encounterBurst = b.Encounter == EncounterKind.Boss ? 1.18d : b.Encounter == EncounterKind.Teleporter ? 1.08d : 1d;
            double block = BlockChance(b.Count("Bear"));
            double safer = b.Count("BearVoid") > 0 ? 0.15d : 0d;
            double opalArmor = b.Count("OutOfCombatArmor") > 0 ? 100d : 0d;
            double conditionalEhp = Valid(b.Health) ? StatCalculator.EffectiveHealth(b.Health, b.Armor + opalArmor) : double.NaN;
            if (!Valid(conditionalEhp)) conditionalEhp = b.EffectiveHealth;
            double burstNeed = Valid(b.NaturalHealth)
                ? b.NaturalHealth * (1d + 0.13d * (stage - 1) + 0.09d * pressure) * encounterBurst : double.NaN;
            if (Valid(conditionalEhp) && Valid(burstNeed))
            {
                double stochastic = 1d + Math.Min(0.75d, block * 0.65d + safer);
                result.Burst = Metric(conditionalEhp * stochastic, burstNeed,
                    "EHP " + F(conditionalEhp) + "; ожидаемый блок " + F(block * 100d) + "%"
                    + (opalArmor > 0 ? "; Opal считается условно активным" : "") + ". Не гарантия против конкретной атаки.");
            }

            double sustainCapacity = 0d;
            if (baseReadiness != null && baseReadiness.Defense.Percent >= 0)
                sustainCapacity += baseReadiness.Defense.Percent / 100d;
            if (Valid(b.Regen) && Valid(b.Health)) sustainCapacity += Math.Min(0.45d, b.Regen / b.Health * 12d);
            sustainCapacity += Math.Min(0.45d, b.Count("Medkit") * 0.06d + b.Count("MushroomVoid") * 0.05d
                + b.Count("HealWhileSafe") * 0.025d + b.Count("SanguineChalice") * 0.04d);
            double sustainNeed = (b.Encounter == EncounterKind.Swarm ? 1.18d : b.Encounter == EncounterKind.Teleporter ? 1.10d : 1d);
            if (sustainCapacity > 0d)
                result.Sustain = Metric(sustainCapacity, sustainNeed,
                    "Защита этапа, регенерация и поддержанные источники восстановления против длительного давления. Условные триггеры лечения не гарантируются.");

            double aoe = 12d + Math.Min(55d, b.LiveAoeItems * 11d);
            aoe += Math.Min(36d, b.Count("ChainLightning") * 12d);
            aoe += Math.Min(34d, b.Count("ExplodeOnDeath") * 11d);
            aoe += Math.Min(30d, b.Count("Behemoth") * 18d);
            aoe += Math.Min(26d, b.Count("Missile") * 6d);
            aoe += Math.Min(20d, b.Count("Dagger") * 8d);
            if (BuildProfileAnalyzer.IsRapidProc(b)) aoe += 12d;
            if (b.Railgunner) aoe -= 8d;
            double clearNeed = b.Encounter == EncounterKind.Swarm ? 72d : b.Encounter == EncounterKind.Teleporter ? 58d : 45d;
            result.Clear = Metric(aoe, clearNeed,
                "Покрытие групп поддержанными AoE/proc-предметами; живые HP врагов и фактический proc coefficient атаки не моделируются.");

            result.Priority = Priority(result, baseReadiness, b.Encounter);
            return result;
        }

        public static StackGuidance Stack(BuildSnapshot b, string id, int count)
        {
            var g = new StackGuidance();
            double marginal = double.NaN;
            if (id == "Crowbar") marginal = RelativeLinear(0.75d, count);
            else if (id == "Syringe") marginal = RelativeLinear(0.15d, count);
            else if (id == "Hoof") marginal = RelativeLinear(0.14d, count);
            else if (id == "SprintBonus") marginal = RelativeLinear(0.25d, count);
            else if (id == "PersonalShield") marginal = RelativeLinear(0.08d, count);
            else if (id == "FlatHealth" && b != null && Valid(b.NaturalHealth))
                marginal = 25d / (b.NaturalHealth + 25d * count) * 100d;
            else if (id == "Bear")
            {
                double before = BlockChance(count), after = BlockChance(count + 1);
                marginal = (after - before) * 100d;
            }

            g.MarginalPercent = marginal;
            if (!ValidNonNegative(marginal))
            {
                if (count >= 10) { g.State = "ПРОВЕРИТЬ"; g.Text = "Стаков уже много; сравни следующий с дефицитами билда."; }
                return g;
            }
            double strong = b != null && b.Style == 2 ? 3d : b != null && b.Style == 0 ? 7d : 5d;
            if (marginal >= strong * 1.6d) g.State = "СИЛЬНЫЙ СТАК";
            else if (marginal >= strong) g.State = "ЕЩЁ ВЫГОДНО";
            else if (marginal >= strong * 0.4d) g.State = "СЛАБАЯ ОТДАЧА";
            else g.State = "ПРЕДЕЛ";
            g.Text = "Следующий стак: примерно +" + F(marginal) + "% относительно уже набранного эффекта"
                + (id == "Crowbar" ? " и только выше 90% HP цели." : ".");
            return g;
        }

        public static string DeathReview(RunReadiness readiness, AdvancedAssessment advanced)
        {
            string area = "данные неполные";
            int lowest = int.MaxValue;
            Consider(ref area, ref lowest, "урон отставал", readiness == null ? null : readiness.Damage);
            Consider(ref area, ref lowest, "слабый первый удар", advanced == null ? null : advanced.Opener);
            Consider(ref area, ref lowest, "низкий темп урона", advanced == null ? null : advanced.Tempo);
            Consider(ref area, ref lowest, "мало HP", readiness == null ? null : readiness.Health);
            Consider(ref area, ref lowest, "слабая защита", readiness == null ? null : readiness.Defense);
            Consider(ref area, ref lowest, "мало мобильности", readiness == null ? null : readiness.Mobility);
            Consider(ref area, ref lowest, "риск сильного удара", advanced == null ? null : advanced.Burst);
            Consider(ref area, ref lowest, "не хватало восстановления", advanced == null ? null : advanced.Sustain);
            Consider(ref area, ref lowest, "слабая зачистка", advanced == null ? null : advanced.Clear);
            return lowest == int.MaxValue ? "Причина по цифрам не определена." : area + " (" + lowest + "%).";
        }

        private static void Consider(ref string area, ref int lowest, string candidate, ReadinessMetric metric)
        {
            if (metric != null && metric.Percent >= 0 && metric.Percent < lowest) { lowest = metric.Percent; area = candidate; }
        }

        private static string Priority(AdvancedAssessment a, RunReadiness r, EncounterKind encounter)
        {
            int burst = a.Burst.Percent, sustain = a.Sustain.Percent, clear = a.Clear.Percent;
            if (burst >= 0 && burst < 85) return "Сначала пережить сильный удар: HP, броня, блок или условная защита.";
            if (sustain >= 0 && sustain < 85) return "Добавить защиту или восстановление для затяжного боя.";
            if ((encounter == EncounterKind.Swarm || encounter == EncounterKind.Teleporter) && clear >= 0 && clear < 90)
                return "Нужна зачистка толпы: AoE, цепи или подходящий proc-эффект.";
            if (a.Opener.Percent >= 0 && a.Opener.Percent < 85) return "Первый сильный удар отстаёт от давления этапа.";
            if (a.Tempo.Percent >= 0 && a.Tempo.Percent < 85) return "Темп урона просел: усилить постоянный DPS или сократить простои.";
            if (r != null && r.Mobility.Percent >= 0 && r.Mobility.Percent < 85) return "Поднять мобильность до продолжения специализации.";
            if (r != null && r.Damage.Percent >= 0 && r.Damage.Percent < 90) return "Урон отстаёт от давления этапа.";
            return "База закрыта: можно усиливать основную синергию персонажа.";
        }

        private static ReadinessMetric Metric(double capacity, double need, string detail)
        {
            int percent = (int)Math.Round(Math.Max(0d, Math.Min(1.5d, capacity / need)) * 100d);
            return new ReadinessMetric { Percent = percent, Status = Status(percent), Detail = detail };
        }

        private static string Status(int percent)
        {
            if (percent < 70) return "МАЛО";
            if (percent < 95) return "ПРОСАДКА";
            if (percent < 120) return "НОРМА";
            return "С ЗАПАСОМ";
        }

        private static string ContextName(EncounterKind kind, int enemies)
        {
            if (kind == EncounterKind.Boss) return "Босс";
            if (kind == EncounterKind.Teleporter) return "Телепортер";
            if (kind == EncounterKind.Swarm) return "Толпа" + (enemies > 0 ? " • врагов " + enemies : "");
            return "Исследование";
        }

        private static double RelativeLinear(double perStack, int count)
        { return perStack / (1d + perStack * Math.Max(0, count)) * 100d; }

        private static double BlockChance(int count)
        { return count <= 0 ? 0d : 0.15d * count / (1d + 0.15d * count); }

        private static bool Valid(double value)
        { return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d; }
        private static bool ValidNonNegative(double value)
        { return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d; }
        private static string F(double value)
        { return value.ToString("0.#", CultureInfo.CurrentCulture); }
    }
}
