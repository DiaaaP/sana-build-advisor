using System;
using System.Collections.Generic;

namespace SanaBuildOverlay
{
    public sealed class EquipmentSnapshot
    {
        public readonly HashSet<string> Available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public string CurrentId = "";
        public string CharacterToken = "";
        public int Style = 1;
        public int Goal;
        public bool CommandMode;
    }

    public sealed class EquipmentAdvice
    {
        public string Id, Reason;
        public double Score;
        public bool AlreadyEquipped;
        public EvidenceStatus Status = EvidenceStatus.Estimated;
    }

    internal sealed class EquipmentRule
    {
        public readonly string Id, Kind, Reason;
        public readonly int Score;
        public EquipmentRule(string id, string kind, int score, string reason)
        { Id = id; Kind = kind; Score = score; Reason = reason; }
    }

    // Advice is conditional: "take it if encountered". It does not predict drops.
    public static class EquipmentAdvisor
    {
        private static readonly EquipmentRule[] Rules = {
            new EquipmentRule("Lightning", "offense", 92,
                "Royal Capacitor: targeted 3000% strike and nearby stun. Strong burst; requires a target."),
            new EquipmentRule("CommandMissile", "offense", 84,
                "Disposable Missile Launcher: 12 missiles for 12x300% damage according to the installed description."),
            new EquipmentRule("Blackhole", "control", 78,
                "Primordial Cube: pulls enemies within 30m for 10 seconds; useful for groups and setup."),
            new EquipmentRule("Fruit", "defense", 80,
                "Foreign Fruit: instantly heals 50% maximum health. Healing restrictions and curses may reduce its value."),
            new EquipmentRule("GainArmor", "defense", 78,
                "Jade Elephant: grants 500 armor for 5 seconds; timing matters."),
            new EquipmentRule("Recycle", "loot", 82,
                "Recycler: rerolls an item or equipment into another of the same tier once. Valuable when choices are random."),
            new EquipmentRule("Scanner", "loot", 62,
                "Radar Scanner: reveals interactables within 500m for 10 seconds; helps find loot, not combat power."),
            new EquipmentRule("BFG", "offense", 94,
                "Preon Accumulator: tendrils damage nearby enemies before a 4000% impact explosion. Long cooldown and projectile travel require timing."),
            new EquipmentRule("CritOnUse", "offense", 82,
                "Ocular HUD: +100% critical strike chance for 8 seconds. Value is lower when ordinary crit chance is already capped or crit rules are unusual."),
            new EquipmentRule("Cleanse", "defense", 84,
                "Blast Shower: removes debuffs, damage over time, stuns, freezes and nearby projectiles. Defensive timing matters."),
            new EquipmentRule("DroneBackup", "control", 72,
                "The Back-up: summons four Strike Drones for 25 seconds. Provides temporary distraction and damage."),
            new EquipmentRule("Jetpack", "mobility", 76,
                "Milky Chrysalis: flight for 15 seconds and +20% movement speed. Strong traversal and emergency vertical mobility."),
            new EquipmentRule("LifestealOnHit", "defense", 79,
                "Super Massive Leech: heals for 20% of damage dealt for 8 seconds. Healing restrictions can reduce or disable it."),
            new EquipmentRule("TeamWarCry", "support", 86,
                "Gorag's Opus: all allies gain +50% movement and +100% attack speed for 7 seconds. Best when the team can use the burst window."),
            new EquipmentRule("Saw", "offense", 80,
                "Sawmerang: three piercing blades deal outgoing, bleeding and return damage. Requires aim and good positioning."),
            new EquipmentRule("Molotov", "offense", 78,
                "Molotov (6-Pack): six cocktails ignite for 500% base damage and leave 200%/s burning areas."),
            new EquipmentRule("GummyClone", "control", 81,
                "Goobo Jr.: creates a 300% damage/health clone for 30 seconds. Performance depends on the copied survivor."),
            new EquipmentRule("MultiShopCard", "loot", 88,
                "Executive Card: refunds 10% of gold purchases and keeps other multishop terminals open. Strong economy, little immediate combat power."),
            new EquipmentRule("HealAndRevive", "support", 91,
                "Seed of Life: revives dead friendly survivors, or revives the holder on death, then is consumed. Team value is situational but unique."),
            new EquipmentRule("DroneShockDamage", "offense", 83,
                "Faulty Conductor: for 5 seconds, survivor and drone hits electrocute for +300% damage every second; electrical damage improves spread chance."),
            new EquipmentRule("GroundEnemies", "control", 70,
                "G-Force Accelerator: grounds and slows enemies within 10m for 10 seconds and adds fall damage when the drop is high enough."),
            new EquipmentRule("Parry", "defense", 90,
                "Deus Ex Machina: a timed parry nullifies incoming damage, cuts equipment cooldown by 75% and primes the next hit's on-hit effects."),
            new EquipmentRule("SpawnAltar", "risky", 64,
                "Totem of Sacrifice: creates a 60-second friendly-fire arena and later rewards kills; it is consumed and can endanger the team.")
        };

        public static IEnumerable<string> KnownIds
        {
            get { foreach (EquipmentRule rule in Rules) yield return rule.Id; }
        }

        public static EquipmentAdvice Evaluate(EquipmentSnapshot snapshot)
        {
            List<EquipmentAdvice> all = EvaluateAll(snapshot);
            return all.Count > 0 ? all[0] : null;
        }

        public static List<EquipmentAdvice> EvaluateAll(EquipmentSnapshot snapshot)
        {
            var result = new List<EquipmentAdvice>();
            if (snapshot == null || snapshot.Available.Count == 0) return result;
            foreach (EquipmentRule rule in Rules)
            {
                if (!snapshot.Available.Contains(rule.Id)) continue;
                double score = rule.Score;
                if (rule.Kind == "offense") score += snapshot.Style == 2 ? 22 : snapshot.Style == 0 ? -10 : 0;
                if (rule.Kind == "defense") score += snapshot.Style == 0 ? 24 : snapshot.Style == 2 ? -8 : 0;
                if (rule.Kind == "control") score += snapshot.Style == 1 ? 8 : 0;
                if (rule.Kind == "loot") score += snapshot.CommandMode ? -24 : 22;
                if (rule.Kind == "mobility") score += snapshot.Style == 0 ? 10 : 4;
                if (rule.Kind == "support") score += snapshot.Style == 0 ? 14 : 6;
                if (rule.Kind == "risky") score += snapshot.Style == 2 ? 16 : snapshot.Style == 0 ? -28 : -8;
                if (snapshot.Goal == 1 && rule.Kind == "offense") score += 24;
                else if (snapshot.Goal == 2 && rule.Kind == "defense") score += 28;
                else if (snapshot.Goal == 2 && rule.Kind == "support") score += 18;
                else if (snapshot.Goal == 3 && (rule.Kind == "control" || rule.Kind == "mobility")) score += 20;
                else if (snapshot.Goal == 4 && rule.Kind == "offense") score += 16;
                int profile = CharacterAdjustment(snapshot.CharacterToken, rule.Id);
                score += profile;
                bool equipped = string.Equals(rule.Id, snapshot.CurrentId, StringComparison.OrdinalIgnoreCase);
                if (equipped) score += 6; // Avoid pointless swaps between nearly equal choices.
                string reason = rule.Reason + (profile == 0 ? "" : " Приоритет скорректирован под выбранного персонажа.");
                var advice = new EquipmentAdvice { Id = rule.Id, Reason = reason, Score = score, AlreadyEquipped = equipped };
                result.Add(advice);
            }
            result.Sort(delegate(EquipmentAdvice a, EquipmentAdvice b) {
                int score = b.Score.CompareTo(a.Score);
                return score != 0 ? score : string.CompareOrdinal(a.Id, b.Id);
            });
            return result;
        }

        // Small, explicit survivor affinities prevent one generic burst item from
        // winning every list. These are advisor heuristics, not measured DPS.
        private static int CharacterAdjustment(string token, string equipmentId)
        {
            token = token ?? "";
            if (token.IndexOf("RAILGUNNER", StringComparison.OrdinalIgnoreCase) >= 0)
                return equipmentId == "CritOnUse" ? 28 : equipmentId == "Blackhole" ? 12 : equipmentId == "Lightning" ? -8 : 0;
            if (token.IndexOf("CAPTAIN", StringComparison.OrdinalIgnoreCase) >= 0)
                return equipmentId == "DroneBackup" ? 30 : equipmentId == "TeamWarCry" ? 10 : 0;
            if (token.IndexOf("HUNTRESS", StringComparison.OrdinalIgnoreCase) >= 0)
                return equipmentId == "CommandMissile" ? 18 : equipmentId == "Jetpack" ? 10 : 0;
            if (token.IndexOf("ENGI", StringComparison.OrdinalIgnoreCase) >= 0)
                return equipmentId == "GummyClone" ? 20 : equipmentId == "Blackhole" ? 14 : 0;
            if (token.IndexOf("MAGE", StringComparison.OrdinalIgnoreCase) >= 0)
                return equipmentId == "Blackhole" ? 22 : equipmentId == "Jetpack" ? 12 : 0;
            if (token.IndexOf("MERC", StringComparison.OrdinalIgnoreCase) >= 0)
                return equipmentId == "GainArmor" ? 22 : equipmentId == "Cleanse" ? 14 : 0;
            if (token.IndexOf("LOADER", StringComparison.OrdinalIgnoreCase) >= 0)
                return equipmentId == "GainArmor" ? 20 : equipmentId == "Parry" ? 14 : 0;
            if (token.IndexOf("CROCO", StringComparison.OrdinalIgnoreCase) >= 0)
                return equipmentId == "Blackhole" ? 20 : equipmentId == "LifestealOnHit" ? 12 : 0;
            if (token.IndexOf("TREEBOT", StringComparison.OrdinalIgnoreCase) >= 0)
                return equipmentId == "Fruit" ? 20 : equipmentId == "LifestealOnHit" ? 14 : 0;
            if (token.IndexOf("BANDIT", StringComparison.OrdinalIgnoreCase) >= 0)
                return equipmentId == "Saw" ? 18 : equipmentId == "CritOnUse" ? -24 : 0;
            if (token.IndexOf("TOOLBOT", StringComparison.OrdinalIgnoreCase) >= 0 || token.IndexOf("COMMANDO", StringComparison.OrdinalIgnoreCase) >= 0)
                return equipmentId == "CommandMissile" ? 16 : equipmentId == "CritOnUse" ? 8 : 0;
            if (token.IndexOf("SEEKER", StringComparison.OrdinalIgnoreCase) >= 0)
                return equipmentId == "TeamWarCry" ? 18 : equipmentId == "HealAndRevive" ? 10 : 0;
            if (token.IndexOf("ACOLYTE", StringComparison.OrdinalIgnoreCase) >= 0)
                return equipmentId == "LifestealOnHit" ? 20 : equipmentId == "Fruit" ? 12 : 0;
            if (token.IndexOf("CHEF", StringComparison.OrdinalIgnoreCase) >= 0)
                return equipmentId == "Molotov" ? 18 : equipmentId == "Saw" ? 10 : 0;
            if (token.IndexOf("FALSESON", StringComparison.OrdinalIgnoreCase) >= 0)
                return equipmentId == "GainArmor" ? 20 : equipmentId == "Parry" ? 12 : 0;
            if (token.IndexOf("VOIDSURVIVOR", StringComparison.OrdinalIgnoreCase) >= 0)
                return equipmentId == "Cleanse" ? 16 : equipmentId == "Blackhole" ? 10 : 0;
            return 0;
        }
    }
}
