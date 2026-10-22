using System;

namespace SanaBuildOverlay
{
    // Pure calculations. Inputs are snapshots, never game objects.
    public static class StatCalculator
    {
        public static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        public static double ClampChance(double percent) { return Math.Max(0, Math.Min(100, percent)); }
        public static double CritGain(double percent)
        {
            return Finite(percent) ? ClampChance(percent + 10) - ClampChance(percent) : double.NaN;
        }
        public static double ExpectedHit(double damage, double chance, double multiplier)
        {
            if (!Finite(damage) || !Finite(chance) || !Finite(multiplier) || damage < 0 || multiplier <= 0)
                return double.NaN;
            return damage * (1 + ClampChance(chance) / 100 * (multiplier - 1));
        }
        // Relative increase of THIS additive item contribution only, not final speed/DPS.
        public static double ItemOnlyGain(double perStack, int count)
        {
            return perStack / (1 + perStack * Math.Max(0, count)) * 100;
        }
        // RoR2 armor multiplier for ordinary damage. Some damage flags bypass armor.
        public static double ArmorDamageMultiplier(double armor)
        {
            if (!Finite(armor)) return double.NaN;
            return armor >= 0 ? 100d / (100d + armor) : 2d - 100d / (100d - armor);
        }
        public static double EffectiveHealth(double healthPool, double armor)
        {
            double multiplier = ArmorDamageMultiplier(armor);
            return !Finite(healthPool) || healthPool < 0 || !Finite(multiplier) || multiplier <= 0
                ? double.NaN : healthPool / multiplier;
        }
    }
}
