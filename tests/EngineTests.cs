using System;
using System.Linq;
using SanaBuildOverlay;

public static class EngineTests
{
    private static int passed;
    private static void Check(bool ok, string name)
    { if (!ok) throw new Exception(name); passed++; }
    private static BuildSnapshot Build()
    {
        var b = new BuildSnapshot { InventoryKnown = true, Crit = 5, Damage = 100, CritMultiplier = 2 };
        foreach (var r in ItemDatabase.Rules) b.Available.Add(r.Id);
        return b;
    }
    private static ItemAdvice Advice(BuildSnapshot b, string id)
    { return RecommendationEngine.Evaluate(b).Find(x => x.Id == id); }
    private static void ApplyReadinessForTest(BuildSnapshot b, RunReadiness r)
    {
        b.DamageReadiness = r.Damage.Percent; b.HealthReadiness = r.Health.Percent;
        b.DefenseReadiness = r.Defense.Percent; b.MobilityReadiness = r.Mobility.Percent;
    }
    public static void Main()
    {
        Check(!InteractionPolicy.WantsOverlayInput(false, false), "hidden overlay never captures input");
        Check(!InteractionPolicy.WantsOverlayInput(false, true), "Alt cannot activate hidden overlay");
        Check(!InteractionPolicy.WantsOverlayInput(true, false), "large HUD is display-only without Alt");
        Check(InteractionPolicy.WantsOverlayInput(true, true), "holding Alt enables overlay input");
        Check(StatCalculator.CritGain(95) == 5, "95% must still gain five points");
        Check(StatCalculator.CritGain(100) == 0 && StatCalculator.CritGain(150) == 0, "crit cap");
        Check(StatCalculator.CritGain(-5) == 5, "negative starting stat clamps correctly");
        Check(double.IsNaN(StatCalculator.CritGain(double.NaN)), "unknown crit stays unknown");
        Check(Math.Abs(StatCalculator.ExpectedHit(100, 25, 2) - 125) < 1e-9, "weighted hit");
        Check(StatCalculator.ExpectedHit(100, 150, 3) == 300, "expected hit clamps cap");
        Check(double.IsNaN(StatCalculator.ExpectedHit(100, 25, double.NaN)), "no assumed x2");
        Check(StatCalculator.ItemOnlyGain(.14, 1) < StatCalculator.ItemOnlyGain(.14, 0), "relative diminishing contribution");
        Check(Math.Abs(StatCalculator.ArmorDamageMultiplier(100) - 0.5) < 1e-9, "100 armor halves ordinary damage");
        Check(Math.Abs(StatCalculator.ArmorDamageMultiplier(-100) - 1.5) < 1e-9, "negative armor amplifies ordinary damage");
        Check(Math.Abs(StatCalculator.EffectiveHealth(100, 100) - 200) < 1e-9, "armor effective health model");
        Check(double.IsNaN(StatCalculator.EffectiveHealth(double.NaN, 0)), "unknown health has no invented EHP");
        var b = Build(); b.InventoryKnown = false;
        Check(RecommendationEngine.Evaluate(b).Count == 0, "unread inventory is not empty inventory");
        b = Build(); b.Available.Clear();
        Check(RecommendationEngine.Evaluate(b).Count == 0, "unavailable pool yields no advice");
        b = Build(); b.Counts["BearVoid"] = 1; b.Counts["CritGlassesVoid"] = 1; b.Counts["ChainLightningVoid"] = 1;
        Check(Advice(b,"Bear") == null && Advice(b,"CritGlasses") == null && Advice(b,"ChainLightning") == null, "three corruption exclusions");
        b = Build(); b.Crit = 95;
        Check(Advice(b,"CritGlasses") != null, "95% not treated as cap");
        b.Crit = 100; Check(Advice(b,"CritGlasses") == null, "100% not recommended");
        b.Crit = double.NaN; Check(Advice(b,"CritGlasses") == null, "unknown chance no normal crit advice");
        b.Railgunner = true; b.Crit = 130;
        Check(Advice(b,"CritGlasses") != null, "Railgunner not ordinary capped crit");
        Check(!Advice(b,"CritGlasses").Reason.Contains("100%:"), "no Railgunner weighted hit claim");
        b = Build(); double regular = Advice(b,"CritGlasses").Score;
        b.Primary = new SkillSnapshot { Id = "HUNTRESS_PRIMARY_ALT_NAME" };
        Check(Advice(b,"CritGlasses").Score > regular, "Flurry selected variant changes priority");
        b.Primary.Id = "HUNTRESS_PRIMARY_NAME";
        Check(Advice(b,"CritGlasses").Score == regular, "no Flurry bonus for Strafe");
        b.CharacterToken = "BANDIT2_BODY_NAME";
        Check(Advice(b,"CritGlasses").Score < regular, "Bandit positioning caveat changes priority");
        b = Build(); Check(Advice(b,"SecondarySkillMagazine") == null, "missing secondary does not produce charge advice");
        b.Secondary = new SkillSnapshot { Recharge = 5, RequiredStock = 1, Name = "Test" };
        Check(Advice(b,"SecondarySkillMagazine") != null && Advice(b,"AlienHead") != null, "recharge skill enables relevant advice");
        b.Secondary.Recharge = 0;
        Check(Advice(b,"SecondarySkillMagazine") == null && Advice(b,"AlienHead") == null, "no invented cooldown benefit");
        b.Utility = new SkillSnapshot { Recharge = 8, RequiredStock = 1 };
        Check(Advice(b,"UtilitySkillMagazine") != null, "utility charges use actual slot");
        b = Build(); double before = Advice(b,"Hoof").Score; b.Counts["Hoof"] = 100;
        Check(Advice(b,"Hoof").Goal == 101 && Advice(b,"Hoof").Score < before, "does not stop after two stacks");
        b.Counts["Hoof"] = int.MaxValue;
        Check(Advice(b,"Hoof") == null, "no int stack overflow");
        b = Build(); before = Advice(b,"Syringe").Score;
        b.Primary = new SkillSnapshot { Recharge = 2, RequiredStock = 1 };
        Check(Advice(b,"Syringe").Score < before, "recharge bottleneck de-prioritizes syringe");
        b.Primary.AttackSpeedRestocks = true;
        Check(Advice(b,"Syringe").Score > before, "read SkillDef restock flag changes assessment");
        b = Build(); string sequence = string.Join(",",RecommendationEngine.Evaluate(b).Select(x=>x.Id));
        Check(sequence == string.Join(",",RecommendationEngine.Evaluate(b).Select(x=>x.Id)), "deterministic ties");
        Check(b.Counts.Count == 0, "evaluation never changes snapshot inventory");
        var ranked = new System.Collections.Generic.List<ItemAdvice> {
            new ItemAdvice { Id = "rare", Tier = "Tier3", Score = 999 },
            new ItemAdvice { Id = "common", Tier = "Tier1", Score = 1 }
        };
        Check(RecommendationEngine.SelectPractical(ranked).Id == "common", "rare long-term item is not compact immediate target");
        ranked.RemoveAt(1);
        Check(RecommendationEngine.SelectPractical(ranked).Id == "rare", "practical selection falls back when common and green are absent");
        Check(RecommendationEngine.SelectPractical(null) == null, "missing ranking has no practical target");

        var eq = new EquipmentSnapshot { Style = 2, CommandMode = false };
        eq.Available.UnionWith(new[] { "Lightning", "Fruit", "Recycle" });
        Check(EquipmentAdvisor.Evaluate(eq).Id == "Lightning", "aggressive equipment favors burst");
        eq.Style = 0;
        Check(EquipmentAdvisor.Evaluate(eq).Id == "Fruit", "safe equipment favors recovery");
        eq.Style = 1;
        Check(EquipmentAdvisor.Evaluate(eq).Id == "Recycle", "random balanced equipment values Recycler");
        eq.CommandMode = true;
        Check(EquipmentAdvisor.Evaluate(eq).Id == "Lightning", "Command reduces Recycler acquisition value");
        eq.CurrentId = "Lightning";
        Check(EquipmentAdvisor.Evaluate(eq).AlreadyEquipped, "current recommended equipment is marked keep");
        eq.Available.Clear();
        Check(EquipmentAdvisor.Evaluate(eq) == null, "unavailable equipment is never recommended");
        eq = new EquipmentSnapshot { Style = 1, Goal = 1 };
        eq.Available.UnionWith(new[] { "Lightning", "Fruit", "Recycle" });
        var allEquipment = EquipmentAdvisor.EvaluateAll(eq);
        Check(allEquipment.Count == 3 && allEquipment[0].Id == "Lightning", "equipment list is complete and goal-ranked");
        Check(allEquipment.All(x => x.Status == EvidenceStatus.Estimated), "equipment evidence status is explicit");
        eq.Goal = 2;
        Check(EquipmentAdvisor.Evaluate(eq).Id == "Fruit", "survival goal changes equipment ranking");
        Check(EquipmentAdvisor.KnownIds.Count() >= 20, "expanded Q equipment knowledge base is loaded");
        eq = new EquipmentSnapshot { Style = 1, CharacterToken = "RAILGUNNER_BODY_NAME" };
        eq.Available.UnionWith(new[] { "Lightning", "CritOnUse" });
        Check(EquipmentAdvisor.Evaluate(eq).Id == "CritOnUse", "Railgunner profile does not default to Capacitor");
        eq = new EquipmentSnapshot { Style = 1, CharacterToken = "CAPTAIN_BODY_NAME" };
        eq.Available.UnionWith(new[] { "Lightning", "DroneBackup" });
        Check(EquipmentAdvisor.Evaluate(eq).Id == "DroneBackup", "Captain profile values drone synergy");
        eq = new EquipmentSnapshot { Style = 1, CharacterToken = "MERC_BODY_NAME" };
        eq.Available.UnionWith(new[] { "Lightning", "GainArmor" });
        Check(EquipmentAdvisor.Evaluate(eq).Id == "GainArmor", "Mercenary profile values close-range defense");
        eq = new EquipmentSnapshot { Style = 1, CharacterToken = "HUNTRESS_BODY_NAME" };
        eq.Available.UnionWith(new[] { "Lightning", "CommandMissile" });
        Check(EquipmentAdvisor.Evaluate(eq).Id == "CommandMissile", "Huntress profile values mobile fire-and-forget burst");
        eq = new EquipmentSnapshot { Style = 0 };
        eq.Available.UnionWith(new[] { "SpawnAltar", "Cleanse" });
        Check(EquipmentAdvisor.Evaluate(eq).Id == "Cleanse", "safe style avoids risky consumable equipment");
        b = Build(); b.Goal = 1;
        double damageGoal = Advice(b, "Syringe").Score;
        b.Goal = 0;
        Check(damageGoal > Advice(b, "Syringe").Score, "damage goal changes item score");
        b.Goal = 2;
        double survivalGoal = Advice(b, "Bear").Score;
        b.Goal = 0;
        Check(survivalGoal > Advice(b, "Bear").Score, "survival goal changes defense score");
        b.Goal = 3;
        double mobilityGoal = Advice(b, "Hoof").Score;
        b.Goal = 0;
        Check(mobilityGoal > Advice(b, "Hoof").Score, "mobility goal changes movement score");
        Check(Advice(b, "Hoof").Status == EvidenceStatus.Calculated && Advice(b, "Feather").Status == EvidenceStatus.Verified,
            "item evidence statuses are explicit");
        Check(ItemDatabase.Rules.Length >= 49, "expanded item knowledge base is loaded");
        Check(ItemDatabase.Rules.Count(x => x.Tier == "Cursed") == 11, "all eleven Hallowed item rules are present");
        Check(ItemDatabase.Rules.Any(x => x.Id == "RechargeableJump" && x.Tier == "Cursed"), "live Cursed jump item is mapped");
        Check(ItemDatabase.Rules.Any(x => x.Id == "BossDamageBonus" && x.Tier == "Tier1"), "live boss damage tier is mapped");
        Check(!ItemDatabase.Rules.Any(x => x.Tier == "Hex"), "harmful Hexes are not recommended as upgrades");
        Check(Advice(b, "VengefulSpirit") != null && Advice(b, "DreamDemon") != null,
            "Hallowed defense and offense choices are scored");
        b = Build(); b.Counts["MushroomVoid"] = 1;
        Check(Advice(b, "Mushroom") == null, "void fungus excludes corrupted base fungus");
        b = Build(); b.Counts["MissileVoid"] = 1;
        Check(Advice(b, "Missile") == null, "plasma shrimp excludes corrupted AtG");
        b = Build(); b.Crit = 0;
        double noCritPredatory = Advice(b, "AttackSpeedOnCrit").Score;
        b.Crit = 25;
        Check(Advice(b, "AttackSpeedOnCrit").Score > noCritPredatory, "crit-triggered item is penalized without crit source");
        Check(Advice(b, "DeathMark").Reason.Contains("4+"), "death mark explains activation requirement");
        b = Build(); b.CharacterToken = "HUNTRESS_BODY_NAME";
        b.Primary = new SkillSnapshot { Id = "HUNTRESS_PRIMARY_ALT_NAME" };
        Check(ArchetypeMatcher.Match(b)[0].Lifecycle == BuildLifecycle.Search, "Flurry crit archetype starts in search");
        b.Counts["CritGlasses"] = 1;
        Check(ArchetypeMatcher.Match(b)[0].Lifecycle == BuildLifecycle.Activation, "first glasses activate Flurry route");
        b.Counts["CritGlasses"] = 5; b.Crit = 100;
        Check(ArchetypeMatcher.Match(b)[0].Lifecycle == BuildLifecycle.Saturation, "Flurry route recognizes crit saturation");
        b.Counts["CritGlassesVoid"] = 1;
        Check(ArchetypeMatcher.Match(b)[0].Lifecycle == BuildLifecycle.Blocked, "Void corruption blocks glasses route");
        b = Build(); b.CharacterToken = "RAILGUNNER_BODY_NAME";
        Check(ArchetypeMatcher.Match(b)[0].Id == "railgunner-crit-conversion", "Railgunner conversion archetype detected");
        var emptyMetrics = BuildProfileAnalyzer.Measure(b);
        Check(!emptyMetrics.CoreReady && emptyMetrics.Offense == 0, "empty build has no fake coverage");
        double earlyCrowbar = Advice(b, "Crowbar").Score;
        b.Counts["Hoof"] = 2; b.Counts["Bear"] = 1; b.Counts["ArmorPlate"] = 1; b.Counts["Crowbar"] = 5;
        var coveredMetrics = BuildProfileAnalyzer.Measure(b);
        Check(coveredMetrics.CoreReady, "basic mobility and defense unlock specialization phase");
        Check(Advice(b, "Crowbar").Score > earlyCrowbar && Advice(b, "Crowbar").Score > Advice(b, "Syringe").Score,
            "covered Railgunner transitions to Crowbar opener specialization");
        b.DamageReadiness = 130; b.HealthReadiness = 97; b.DefenseReadiness = 82; b.MobilityReadiness = 105;
        b.Counts["Crowbar"] = 89;
        Check(Advice(b, "Bear").Score > Advice(b, "Crowbar").Score,
            "huge Crowbar stack yields to defense when stage survival is behind");
        Check(Advice(b, "Crowbar").Reason.Contains("примерно +1,1%") ||
            Advice(b, "Crowbar").Reason.Contains("примерно +1.1%"),
            "Crowbar advice exposes the tiny marginal gain at stack 89");
        b.Counts["Crowbar"] = 5; b.HealthReadiness = 105; b.DefenseReadiness = 105;
        Check(Advice(b, "Crowbar").Score > Advice(b, "Bear").Score,
            "healthy covered Railgunner can still pursue a moderate Crowbar specialization");
        b.Counts.Clear(); b.Counts["Crowbar"] = 100000;
        var oneStackMetrics = BuildProfileAnalyzer.Measure(b);
        Check(oneStackMetrics.Offense <= 100 && oneStackMetrics.Defense == 0 && oneStackMetrics.Mobility == 0 && !oneStackMetrics.CoreReady,
            "huge single stack cannot masquerade as a complete build");
        var earlyReadiness = ReadinessAnalyzer.Evaluate(0, 1, 12, 12, 110, 110, 110, 7, 7, new BuildMetrics());
        Check(earlyReadiness.Damage.Percent == 100 && earlyReadiness.Health.Percent == 100 && earlyReadiness.Mobility.Percent == 100,
            "natural stats are adequate baseline on stage one");
        var lateReadiness = ReadinessAnalyzer.Evaluate(4, 4, 12, 12, 110, 110, 110, 7, 7, new BuildMetrics());
        Check(lateReadiness.Damage.Percent < 70 && lateReadiness.Health.Percent < 95 && lateReadiness.Mobility.Percent < 95,
            "unchanged baseline falls behind live stage and difficulty pressure");
        var upgradedReadiness = ReadinessAnalyzer.Evaluate(4, 4, 24, 12, 220, 110, 300, 10.5, 7,
            new BuildMetrics { Offense = 70, Defense = 60, Mobility = 60 });
        Check(upgradedReadiness.Damage.Percent >= 95 && upgradedReadiness.Health.Percent >= 95 && upgradedReadiness.Defense.Percent >= 95,
            "real stat and coverage upgrades recover stage readiness");
        var unknownReadiness = ReadinessAnalyzer.Evaluate(-1, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN, null);
        Check(unknownReadiness.Damage.Percent == -1 && unknownReadiness.Defense.Status == "НЕТ ДАННЫХ",
            "missing run data stays unknown instead of fake zero");
        b = Build(); b.Style = 1;
        var freshStack = AdvancedAdvisor.Stack(b, "Crowbar", 0);
        var lateStack = AdvancedAdvisor.Stack(b, "Crowbar", 89);
        Check(freshStack.MarginalPercent > 70 && lateStack.MarginalPercent < 1.2 && lateStack.State == "ПРЕДЕЛ",
            "stack advisor exposes relative Crowbar saturation");
        b.Style = 0;
        string safeStack = AdvancedAdvisor.Stack(b, "Crowbar", 20).State;
        b.Style = 2;
        Check(safeStack == "СЛАБАЯ ОТДАЧА" && AdvancedAdvisor.Stack(b, "Crowbar", 20).State == "ЕЩЁ ВЫГОДНО",
            "risk style changes soft stack limit without changing mechanics");
        b = Build(); b.Stage = 1; b.Difficulty = 1; b.Health = 110; b.NaturalHealth = 110; b.EffectiveHealth = 110;
        b.NaturalDamage = 100; b.AttackSpeed = 1; b.Armor = 0; b.Regen = 1; b.Encounter = EncounterKind.Exploration;
        var baseAdvanced = AdvancedAdvisor.Evaluate(b, new BuildMetrics(), earlyReadiness);
        b.CharacterToken = "RAILGUNNER_BODY_NAME"; b.Counts["Crowbar"] = 3;
        Check(AdvancedAdvisor.Evaluate(b, new BuildMetrics(), earlyReadiness).Opener.Percent > baseAdvanced.Opener.Percent,
            "heavy opener proxy reacts to Crowbar on a burst profile");
        b.CharacterToken = "COMMANDO_BODY_NAME"; b.Counts.Clear(); b.AttackSpeed = 1.8;
        Check(AdvancedAdvisor.Evaluate(b, new BuildMetrics(), earlyReadiness).Tempo.Percent > baseAdvanced.Tempo.Percent,
            "damage tempo proxy reacts to live attack speed");
        b.AttackSpeed = 1;
        b.Counts["Bear"] = 10;
        var blockedAdvanced = AdvancedAdvisor.Evaluate(b, new BuildMetrics(), earlyReadiness);
        Check(blockedAdvanced.Burst.Percent > baseAdvanced.Burst.Percent,
            "burst survival model reacts to supported block stacks");
        int emptyClear = baseAdvanced.Clear.Percent;
        b.Counts["ChainLightning"] = 3;
        Check(AdvancedAdvisor.Evaluate(b, new BuildMetrics(), earlyReadiness).Clear.Percent > emptyClear,
            "clear model reacts to supported AoE items");
        b.Encounter = EncounterKind.Swarm; b.Counts.Clear();
        var swarmAdvanced = AdvancedAdvisor.Evaluate(b, new BuildMetrics(), earlyReadiness);
        Check(swarmAdvanced.Priority.Contains("зачист"), "swarm context calls out inadequate clear");
        b = Build(); b.Encounter = EncounterKind.Exploration;
        double exploreChain = Advice(b, "ChainLightning").Score;
        b.Encounter = EncounterKind.Swarm;
        Check(Advice(b, "ChainLightning").Score > exploreChain, "swarm context raises AoE recommendation priority");
        var deathAdvanced = new AdvancedAssessment { Burst = new ReadinessMetric { Percent = 100 },
            Sustain = new ReadinessMetric { Percent = 90 }, Clear = new ReadinessMetric { Percent = 35 } };
        Check(AdvancedAdvisor.DeathReview(earlyReadiness, deathAdvanced).Contains("зачистка"),
            "death review reports the weakest measured axis");
        b = Build(); b.CharacterToken = "RAILGUNNER_BODY_NAME"; b.Stage = 1; b.Difficulty = 1;
        b.NaturalDamage = b.Damage = 100; b.AttackSpeed = 1; b.Health = b.NaturalHealth = b.EffectiveHealth = 110; b.Regen = 1;
        ApplyReadinessForTest(b, earlyReadiness);
        var decisionBase = AdvancedAdvisor.Evaluate(b, BuildProfileAnalyzer.Measure(b), earlyReadiness);
        var crowbarImpact = DecisionAdvisor.Simulate(b, "Crowbar", earlyReadiness, decisionBase);
        Check(crowbarImpact.Opener > 0 && b.Count("Crowbar") == 0, "what-if Crowbar preview improves opener without mutating inventory");
        var clearImpact = DecisionAdvisor.Simulate(b, "ChainLightning", earlyReadiness, decisionBase);
        Check(clearImpact.Clear > 0 && clearImpact.Summary.Contains("зач"), "what-if AoE preview exposes clear gain");
        b.Counts["Crowbar"] = 5;
        var burstDecision = DecisionAdvisor.Analyze(b, BuildProfileAnalyzer.Measure(b), earlyReadiness,
            AdvancedAdvisor.Evaluate(b, BuildProfileAnalyzer.Measure(b), earlyReadiness), RecommendationEngine.Evaluate(b));
        Check(burstDecision.BuildDna.Contains("ВАНШОТ") && burstDecision.TeamRole == "Убийца боссов",
            "decision report recognizes burst DNA and team role");
        b.DamageReadiness = 145; b.HealthReadiness = 70; b.DefenseReadiness = 75;
        Check(DecisionAdvisor.Analyze(b, BuildProfileAnalyzer.Measure(b), earlyReadiness, decisionBase,
            RecommendationEngine.Evaluate(b)).Overinvestment.Contains("перекачан"), "decision report warns about damage overinvestment");
        var readyAdvanced = new AdvancedAssessment { Opener = new ReadinessMetric { Percent = 120 }, Tempo = new ReadinessMetric { Percent = 120 },
            Burst = new ReadinessMetric { Percent = 110 }, Sustain = new ReadinessMetric { Percent = 110 }, Clear = new ReadinessMetric { Percent = 100 } };
        Check(DecisionAdvisor.Analyze(b, BuildProfileAnalyzer.Measure(b), earlyReadiness, readyAdvanced,
            RecommendationEngine.Evaluate(b)).Teleporter.Contains("можно запускать"), "teleporter advisor recognizes covered build");
        Check(DecisionAdvisor.Analyze(b, BuildProfileAnalyzer.Measure(b), earlyReadiness, readyAdvanced,
            RecommendationEngine.Evaluate(b)).LootFit.EndsWith("%"), "loot fit report stays numeric and explicit");
        b = Build(); b.LiveDamageItems = 5; b.LiveAoeItems = 3;
        var liveMetrics = BuildProfileAnalyzer.Measure(b);
        Check(liveMetrics.Offense > 0 && liveMetrics.Synergy > 0, "live catalog tags contribute conservative build coverage");
        Check(oneStackMetrics.Defense == 0 && oneStackMetrics.Mobility == 0,
            "conditional burst stacks do not inflate health or mobility readiness inputs");
        b = Build(); b.CharacterToken = "COMMANDO_BODY_NAME";
        Check(ArchetypeMatcher.Match(b).Any(x => x.Id == "adaptive-proc"), "generic Commando profile has a usable proc route");
        double earlyMissile = Advice(b, "Missile").Score;
        b.Counts["Hoof"] = 2; b.Counts["Bear"] = 1; b.Counts["ArmorPlate"] = 1;
        Check(Advice(b, "Missile").Score > earlyMissile, "covered rapid-fire profile transitions toward proc specialization");
        b = Build(); b.CharacterToken = "RAILGUNNER_BODY_NAME";
        double earlyBand = Advice(b, "FireRing").Score;
        b.Counts["Hoof"] = 2; b.Counts["Bear"] = 1; b.Counts["ArmorPlate"] = 1;
        Check(Advice(b, "FireRing").Score > earlyBand, "covered Railgunner values heavy-hit band synergy");
        b = Build(); b.CharacterToken = "TOOLBOT_BODY_NAME"; b.Primary = new SkillSnapshot { Id = "TOOLBOT_PRIMARY_ALT1_NAME" };
        double rebarCrowbar = Advice(b, "Crowbar").Score;
        b.Counts["Hoof"] = 2; b.Counts["Bear"] = 1; b.Counts["ArmorPlate"] = 1;
        Check(Advice(b, "Crowbar").Score > rebarCrowbar, "MUL-T Rebar enters heavy-hit specialization after core coverage");
        b = Build(); b.CharacterToken = "TOOLBOT_BODY_NAME"; b.Primary = new SkillSnapshot { Id = "TOOLBOT_PRIMARY_NAME" };
        double nailgunMissile = Advice(b, "Missile").Score;
        b.Counts["Hoof"] = 2; b.Counts["Bear"] = 1; b.Counts["ArmorPlate"] = 1;
        Check(Advice(b, "Missile").Score > nailgunMissile, "MUL-T Nailgun enters rapid proc specialization");
        b = Build(); b.Railgunner = true; b.CharacterToken = "RAILGUNNER_BODY_NAME";
        b.Secondary = new SkillSnapshot { Id = "RAILGUNNER_SECONDARY_ALT_NAME", Name = "HH44", Recharge = 1, RequiredStock = 1 };
        Check(Advice(b, "SecondarySkillMagazine") == null, "HH44 does not receive a fake Backup Magazine recommendation");
        b.Secondary = new SkillSnapshot { Id = "RAILGUNNER_SECONDARY_NAME", Name = "M99", Recharge = 1, RequiredStock = 1 };
        Check(Advice(b, "SecondarySkillMagazine") != null, "M99 keeps its useful Backup Magazine route");
        b = Build(); double genericFungus = Advice(b, "Mushroom").Score;
        b.CharacterToken = "ENGI_BODY_NAME"; b.Special = new SkillSnapshot { Id = "ENGI_SPECIAL_NAME" };
        Check(Advice(b, "Mushroom").Score > genericFungus && ArchetypeMatcher.Match(b).Any(x => x.Id == "engineer-stationary-turret"),
            "stationary Engineer recognizes inherited Bustling Fungus route");
        b = Build(); b.CharacterToken = "SEEKER_BODY_NAME";
        b.Utility = new SkillSnapshot { Id = "SEEKER_UTILITY_NAME", Name = "Sojourn", Recharge = 6, RequiredStock = 1 };
        var seekerRoute = ArchetypeMatcher.Match(b)[0];
        Check(seekerRoute.Id == "seeker-sojourn-lunar-burst" && !seekerRoute.Verified && seekerRoute.Lifecycle == BuildLifecycle.Search,
            "Seeker Sojourn route is explicitly an unverified search hypothesis");
        b.Counts["ShieldOnly"] = 1;
        Check(ArchetypeMatcher.Match(b)[0].Lifecycle == BuildLifecycle.Activation, "Transcendence activates Seeker hypothesis foundation");
        b = Build(); b.CharacterToken = "ACOLYTE_BODY_NAME";
        var acolyteRoute = ArchetypeMatcher.Match(b)[0];
        Check(acolyteRoute.Id == "acolyte-transcendence" && !acolyteRoute.Verified,
            "Acolyte Transcendence route remains explicitly experimental");
        Check(Advice(b, "Medkit").Reason.Contains("не восстанавливает HP"), "Acolyte healing conversion changes advice explanation");
        Check(ArchetypeMatcher.Match(null).Count == 0, "missing snapshot has no archetype");
        var memberA = new TeamMemberSnapshot { UserName = "A", CharacterName = "One", InventoryVisible = true, Build = Build() };
        var memberB = new TeamMemberSnapshot { UserName = "B", CharacterName = "Two", InventoryVisible = true, Build = Build() };
        memberA.Alive = memberB.Alive = true;
        memberA.Readiness = new RunReadiness { Damage = new ReadinessMetric { Percent = 45 }, Health = new ReadinessMetric { Percent = 100 },
            Defense = new ReadinessMetric { Percent = 95 }, Mobility = new ReadinessMetric { Percent = 90 } };
        memberB.Readiness = new RunReadiness { Damage = new ReadinessMetric { Percent = 120 }, Health = new ReadinessMetric { Percent = 105 },
            Defense = new ReadinessMetric { Percent = 100 }, Mobility = new ReadinessMetric { Percent = 100 } };
        TeamAnalyzer.Summarize(memberA); TeamAnalyzer.Summarize(memberB);
        Check(memberA.WeakestArea == "урон" && memberA.OverallReadiness < memberB.OverallReadiness,
            "team summary exposes each player's limiting category");
        Check(TeamAnalyzer.FindWeakest(new[] { memberB, memberA }) == memberA,
            "team view identifies weakest stage readiness deterministically");
        var deficitDistribution = TeamAnalyzer.ChooseRecipient("Crowbar", new[] { memberB, memberA });
        Check(deficitDistribution != null && deficitDistribution.UserName == "A" && deficitDistribution.TargetArea == "урон",
            "offense pickup allocation favors relevant team deficit");
        memberB.Build.Counts["Hoof"] = 8;
        var distribution = TeamAnalyzer.ChooseRecipient("Hoof", new[] { memberB, memberA });
        Check(distribution != null && distribution.UserName == "A", "team advisor gives item to higher marginal score");
        Check(TeamAnalyzer.ChooseRecipient("unknown", new[] { memberA }) == null, "team advisor does not invent unsupported item scores");
        memberA.Readiness = memberB.Readiness = null;
        memberA.OverallReadiness = memberB.OverallReadiness = -1;
        memberA.UserName = "B"; memberB.UserName = "A"; memberB.Build.Counts.Clear();
        distribution = TeamAnalyzer.ChooseRecipient("Hoof", new[] { memberA, memberB });
        Check(distribution.UserName == "A", "team tie break is deterministic");
        var hiddenA = new TeamMemberSnapshot { UserName = "Hidden1", InventoryVisible = false };
        var hiddenB = new TeamMemberSnapshot { UserName = "Hidden2", InventoryVisible = false };
        Check(TeamAnalyzer.ChooseRecipient("Hoof", new[] { hiddenA, hiddenB }) == null,
            "missing remote inventories never produce a recipient");
        distribution = TeamAnalyzer.ChooseRecipient("Hoof", new[] { hiddenA, memberA, hiddenB, memberB });
        Check(distribution != null && distribution.UserName == "A", "four-player list skips unavailable inventories");
        b = Build(); b.Available.Add("UnverifiedDlcItem");
        Check(Advice(b, "UnverifiedDlcItem") == null, "unknown DLC item is never assigned a fabricated rule");
        b = Build(); b.Style = 2; b.Goal = 1;
        string firstRun = string.Join(",", RecommendationEngine.Evaluate(b).Select(x => x.Id));
        b.Counts["Syringe"] = 4;
        string secondRun = string.Join(",", RecommendationEngine.Evaluate(b).Select(x => x.Id));
        Check(firstRun != secondRun, "new run snapshot state recalculates recommendation order");
        Console.WriteLine("PASS: " + passed + " assertions");
    }
}
