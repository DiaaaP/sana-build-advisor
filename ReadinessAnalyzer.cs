using System;
using System.Globalization;

namespace SanaBuildOverlay
{
    public sealed class ReadinessMetric
    {
        public int Percent = -1;
        public string Status = "НЕТ ДАННЫХ";
        public string Detail = "Недостаточно числовых данных из игры.";
    }

    public sealed class RunReadiness
    {
        public int Stage;
        public double Difficulty;
        public ReadinessMetric Damage = new ReadinessMetric();
        public ReadinessMetric Health = new ReadinessMetric();
        public ReadinessMetric Defense = new ReadinessMetric();
        public ReadinessMetric Mobility = new ReadinessMetric();
    }

    // A transparent heuristic, not a claim about exact time-to-kill or survival.
    // Natural stats remove survivor/level differences; stage and the live
    // difficulty coefficient raise the target as the run becomes more dangerous.
    public static class ReadinessAnalyzer
    {
        public static RunReadiness Evaluate(int stagesCompleted, double difficulty,
            double damage, double naturalDamage, double health, double naturalHealth,
            double effectiveHealth, double moveSpeed, double naturalMoveSpeed, BuildMetrics coverage)
        {
            var result = new RunReadiness { Stage = Math.Max(1, stagesCompleted + 1), Difficulty = difficulty };
            if (stagesCompleted < 0 || !ValidPositive(difficulty)) return result;
            coverage = coverage ?? new BuildMetrics();
            double pressure = Math.Log(Math.Max(1d, difficulty), 2d);

            double damageNeed = 1d + 0.20d * stagesCompleted + 0.10d * pressure;
            if (ValidPositive(damage) && ValidPositive(naturalDamage))
            {
                double capacity = damage / naturalDamage * (1d + coverage.Offense / 100d * 1.35d);
                result.Damage = Metric(capacity, damageNeed,
                    "Сила " + F(capacity) + "x / цель " + F(damageNeed) + "x. Условные бонусы вроде Crowbar учитываются как покрытие, не как постоянный DPS.");
            }

            double healthNeed = 1d + 0.09d * stagesCompleted + 0.07d * pressure;
            if (ValidPositive(health) && ValidPositive(naturalHealth))
            {
                double capacity = health / naturalHealth;
                result.Health = Metric(capacity, healthNeed,
                    "HP+щит " + F(health) + " / ориентир " + F(naturalHealth * healthNeed) + ". Барьер не считается постоянным HP.");
            }

            double defenseNeed = 1d + 0.14d * stagesCompleted + 0.09d * pressure;
            if (ValidPositive(effectiveHealth) && ValidPositive(naturalHealth))
            {
                double capacity = effectiveHealth / naturalHealth * (1d + coverage.Defense / 100d * 0.50d);
                result.Defense = Metric(capacity, defenseNeed,
                    "Защитная сила " + F(capacity) + "x / цель " + F(defenseNeed) + "x. EHP относится к обычному урону; блок и лечение добавлены как покрытие.");
            }

            double mobilityNeed = 1d + 0.055d * stagesCompleted + 0.025d * pressure;
            if (ValidPositive(moveSpeed) && ValidPositive(naturalMoveSpeed))
            {
                double capacity = moveSpeed / naturalMoveSpeed * (1d + coverage.Mobility / 100d * 0.12d);
                result.Mobility = Metric(capacity, mobilityNeed,
                    "Мобильность " + F(capacity) + "x / цель " + F(mobilityNeed) + "x; скорость " + F(moveSpeed) + ". Прыжки учитываются через покрытие предметов.");
            }
            return result;
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

        private static bool ValidPositive(double value)
        { return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d; }

        private static string F(double value)
        { return value.ToString("0.##", CultureInfo.CurrentCulture); }
    }
}
