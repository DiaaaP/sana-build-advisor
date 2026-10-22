namespace SanaBuildOverlay
{
    public enum EvidenceStatus { Verified, Calculated, Estimated, Experimental, Unknown }

    public sealed class ItemRule
    {
        public readonly string Id, Tier, Category, CorruptedBy;
        public readonly int Priority;
        public readonly EvidenceStatus Status;
        public ItemRule(string id, string tier, string category, string corruptedBy, int priority,
            EvidenceStatus status = EvidenceStatus.Estimated)
        { Id = id; Tier = tier; Category = category; CorruptedBy = corruptedBy; Priority = priority; Status = status; }
    }

    // IDs and tiers are checked against ItemCatalog before presentation.
    // Effects: installed en/Items.json; skill evidence: en/CharacterBodies.json.
    // Priorities are explicit advisor heuristics, never measured DPS.
    public static class ItemDatabase
    {
        public static readonly ItemRule[] Rules = {
            new ItemRule("Hoof", "Tier1", "movement", null, 80, EvidenceStatus.Calculated),
            new ItemRule("SprintBonus", "Tier1", "movement", null, 76, EvidenceStatus.Calculated),
            new ItemRule("Bear", "Tier1", "defense", "BearVoid", 76),
            new ItemRule("ArmorPlate", "Tier1", "defense", null, 70, EvidenceStatus.Verified),
            new ItemRule("FlatHealth", "Tier1", "defense", null, 68, EvidenceStatus.Calculated),
            new ItemRule("PersonalShield", "Tier1", "defense", null, 66, EvidenceStatus.Calculated),
            new ItemRule("Medkit", "Tier1", "healing", null, 66, EvidenceStatus.Verified),
            new ItemRule("Mushroom", "Tier1", "healing", "MushroomVoid", 58, EvidenceStatus.Verified),
            new ItemRule("HealWhileSafe", "Tier1", "healing", null, 62, EvidenceStatus.Verified),
            new ItemRule("Syringe", "Tier1", "offense", null, 70, EvidenceStatus.Verified),
            new ItemRule("CritGlasses", "Tier1", "offense", "CritGlassesVoid", 72, EvidenceStatus.Calculated),
            new ItemRule("Crowbar", "Tier1", "offense", null, 75, EvidenceStatus.Verified),
            new ItemRule("BleedOnHit", "Tier1", "offense", "BleedOnHitVoid", 70, EvidenceStatus.Verified),
            new ItemRule("StickyBomb", "Tier1", "offense", null, 64, EvidenceStatus.Verified),
            new ItemRule("SecondarySkillMagazine", "Tier1", "skill", null, 74, EvidenceStatus.Verified),

            new ItemRule("Feather", "Tier2", "movement", null, 84, EvidenceStatus.Verified),
            new ItemRule("SprintArmor", "Tier2", "defense", null, 73, EvidenceStatus.Verified),
            new ItemRule("BossDamageBonus", "Tier1", "offense", null, 79, EvidenceStatus.Verified),
            new ItemRule("ChainLightning", "Tier2", "offense", "ChainLightningVoid", 78),
            new ItemRule("Missile", "Tier2", "offense", "MissileVoid", 82, EvidenceStatus.Verified),
            new ItemRule("ExplodeOnDeath", "Tier2", "offense", "ExplodeOnDeathVoid", 72, EvidenceStatus.Verified),
            new ItemRule("SlowOnHit", "Tier2", "control", "SlowOnHitVoid", 60, EvidenceStatus.Verified),
            new ItemRule("NearbyDamageBonus", "Tier1", "offense", null, 70, EvidenceStatus.Verified),
            new ItemRule("AttackSpeedOnCrit", "Tier2", "offense", null, 69, EvidenceStatus.Verified),
            new ItemRule("DeathMark", "Tier2", "offense", null, 67, EvidenceStatus.Verified),
            new ItemRule("EquipmentMagazine", "Tier2", "skill", null, 70, EvidenceStatus.Verified),
            new ItemRule("FireRing", "Tier2", "offense", "ElementalRingVoid", 80, EvidenceStatus.Verified),
            new ItemRule("IceRing", "Tier2", "offense", "ElementalRingVoid", 78, EvidenceStatus.Verified),

            new ItemRule("AlienHead", "Tier3", "skill", null, 80),
            new ItemRule("UtilitySkillMagazine", "Tier3", "skill", null, 78, EvidenceStatus.Verified),
            new ItemRule("Dagger", "Tier3", "offense", null, 82, EvidenceStatus.Verified),
            new ItemRule("Behemoth", "Tier3", "offense", null, 88, EvidenceStatus.Verified),
            new ItemRule("Clover", "Tier3", "utility", null, 84, EvidenceStatus.Verified),

            new ItemRule("ShieldOnly", "Lunar", "defense", null, 60, EvidenceStatus.Estimated),
            new ItemRule("LunarDagger", "Lunar", "offense", null, 72, EvidenceStatus.Estimated),

            new ItemRule("MushroomVoid", "VoidTier1", "healing", null, 74, EvidenceStatus.Verified),
            new ItemRule("BleedOnHitVoid", "VoidTier1", "offense", null, 76, EvidenceStatus.Verified),
            new ItemRule("MissileVoid", "VoidTier2", "offense", null, 82, EvidenceStatus.Verified),
            new ItemRule("SlowOnHitVoid", "VoidTier2", "control", null, 64, EvidenceStatus.Verified),
            new ItemRule("ExplodeOnDeathVoid", "VoidTier2", "offense", null, 78, EvidenceStatus.Verified),
            new ItemRule("RechargeableJump", "Cursed", "movement", null, 74),
            new ItemRule("BlightZone", "Cursed", "offense", null, 74),
            new ItemRule("SprintDamage", "Cursed", "movement", null, 76),
            new ItemRule("GhostBlades", "Cursed", "offense", null, 78),
            new ItemRule("CausticPheromones", "Cursed", "control", null, 68),
            new ItemRule("DreamDemon", "Cursed", "offense", null, 82),
            new ItemRule("SanguineChalice", "Cursed", "healing", null, 74),
            new ItemRule("VengefulSpirit", "Cursed", "defense", null, 86),
            new ItemRule("LowHealthOrbSpawner", "Cursed", "utility", null, 62),
            new ItemRule("GoldenGoose", "Cursed", "control", null, 73),
            new ItemRule("VileCenser", "Cursed", "offense", null, 77)
        };
    }
}
