using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace SanaBuildOverlay
{
    [BepInPlugin("local.sana.ror2.buildoverlay", "Sana Build Overlay", "0.14.3")]
    public sealed partial class BuildOverlay : BaseUnityPlugin
    {
        private const int WindowId = 731804;
        private AdvisorSettings settings;
        private bool visible = true;
        private bool expanded;
        private bool iconOnly; // one-icon mode: smallest footprint while fighting
        private bool commandMode;
        private bool automaticMode = true;
        private string modeEvidence = "Нет активного забега";
        private BuildSnapshot snapshot;
        private BuildMetrics buildMetrics;
        private RunReadiness runReadiness;
        private AdvancedAssessment advancedAssessment;
        private DecisionReport decisionReport;
        private string lastDecisionNotice = "";
        private int effectiveRiskStyle = 1;
        private string nearbyFacilityAdvice = "Рядом нет поддержанного принтера, утилизатора или котла.";
        private string reservedItemId = "", reservedItemName = "";
        private readonly List<RunTrendPoint> runTrend = new List<RunTrendPoint>();
        private object trendRunIdentity;
        private float nextTrendCapture;
        private string runReport = "История текущего забега ещё не накоплена.";
        private string lastFatalHit = "";
        private string nextStageForecast = "Следующий этап ещё не рассчитан.";
        private string teamGapSummary = "Командный пробел не рассчитан.";
        private double trackedDamage;
        private int trackedHits;
        private double biggestTrackedHit;
        private readonly List<Recommendation> rankedAdvice = new List<Recommendation>();
        private static readonly Dictionary<string, string> liveTagCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> liveCategoryCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> liveAoeCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private Recommendation bestRare;
        private Recommendation bestLongTerm;
        private EquipmentAdvice bestEquipmentAdvice;
        private string currentEquipmentId = "";
        private string currentEquipmentName = "Пусто";
        private string recommendedEquipmentName = "Нет проверенного совета";
        private string recommendedEquipmentReason = "Список Q-снаряжения пока ограничен.";
        private Sprite recommendedEquipmentIcon;
        private ArchetypeMatch activeArchetype;
        private string lastArchetypeId = "";
        private string strategyNotice = "";
        private float strategyNoticeUntil;
        private readonly List<TeamMemberSnapshot> teamMembers = new List<TeamMemberSnapshot>();
        private TeamMemberSnapshot weakestTeamMember;
        private readonly List<NearbyOffer> nearbyOffers = new List<NearbyOffer>();
        private readonly List<NearbyOffer> cachedChoiceOffers = new List<NearbyOffer>();
        private readonly List<ItemAdvice> cachedLocalAdvice = new List<ItemAdvice>();
        private readonly List<EquipmentAdvice> cachedEquipmentAdvice = new List<EquipmentAdvice>();
        private readonly Dictionary<string, string> nearbyImpactCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private NearbyOffer bestAvailable;
        private string nearbyStatus = "Лут рядом ещё не прочитан";
        private float nextLootRefreshTime;
        private float nextChoiceLootRefreshTime;
        private float nextIconRefreshTime;
        private long nearbyImpactFingerprint = long.MinValue;
        private int iconRefreshCursor;
        private TeamDistribution teamDistribution;
        private string teamStatus = "Команда ещё не прочитана";
        private float nextTeamRefreshTime;
        private float nextThreatRefreshTime;
        private float nextFacilityRefreshTime;
        private bool errorLogged;
        private bool catalogValidated;
        private Rect windowRect = new Rect(42f, 65f, 322f, 158f);
        private float nextRefreshTime;
        private float nextInventoryRefreshTime;
        private float nextSkillRefreshTime;
        private long inventoryFingerprint;
        private long lastAdviceFingerprint = long.MinValue;
        private Vector2 detailsScroll;
        private int page; // 0 Stats, 1 Skills, 2 Inventory, 3 Plan, 4 Run
        private string selectedItemId;
        private int playStyle = 1; // 0 safe, 1 balanced, 2 aggressive. Only affects heuristic priorities.
        private int buildGoal; // 0 balanced, 1 damage, 2 survival, 3 mobility, 4 OP, 5 detected/custom.

        // Live run context. Unknown values remain unknown instead of using guessed defaults.
        private string stageCompleted = "—";
        private string runTime = "—";
        private string difficultyName = "—";
        private string difficultyCoefficient = "—";
        private string partyCount = "—";
        private string partyEvidence = "Недоступно";
        private int knownPartyCount;
        private int knownStagesCompleted = -1;
        private float knownDifficultyCoefficient = -1f;
        private string currentMoney = "—";
        private string interactableBudget = "—";
        private string approximateShare = "—";
        private string mobilityCoverage = "Нет данных";
        private string defenseCoverage = "Нет данных";
        private string offenseCoverage = "Нет данных";
        private string supportCoverage = "Нет данных";
        private string nextStageWarning = "Недостаточно данных для прогноза";
        private string remoteVisibility = "Не проверено";
        private string activeArtifacts = "—";
        private EncounterKind encounterKind;
        private int hostileCount;
        private string encounterSummary = "Исследование";
        private string dangerRadar = "Опасная цель не определена.";
        private string lastDeathReview = "Смертей в этой истории пока нет.";
        private int deathReviewCount;
        private string deathHistoryPath;
        private string runHistoryPath;
        private bool observedAlive;
        private float lastObservedHealthFraction = 1f;

        private bool hasCharacter;
        private string characterName = "Нет активного персонажа";
        private string level = "—";
        private string hp = "—";
        private string shield = "—";
        private string barrier = "—";
        private string damage = "—";
        private string critStat = "—";
        private string critChance = "—";
        private string critDamage = "—";
        private string expectedDamage = "—";
        private string critMultiplier = "—";
        private string attackSpeed = "—";
        private string armor = "—";
        private string moveSpeed = "—";
        private string regen = "—";
        private string armorEffect = "—";
        private string effectiveHealth = "—";
        private string oneShotProtection = "—";
        private string activeBuffs = "—";
        private string activeDebuffs = "—";
        private string nextItemText = "Рекомендации появятся после начала забега";
        private string nextItemReason = "Используются только подтверждённые правила для отдельных предметов.";
        private string critDisclaimer = "Обычная модель критов";
        private Texture portrait;
        private bool isRailgunner;
        private bool hasCritStat;
        private float critPercentCurrent;
        private double currentDamageRaw = double.NaN;
        private double naturalDamageRaw = double.NaN;
        private double currentHealthRaw = double.NaN;
        private double naturalHealthRaw = double.NaN;
        private double currentEffectiveHealthRaw = double.NaN;
        private double currentMoveSpeedRaw = double.NaN;
        private double naturalMoveSpeedRaw = double.NaN;
        private bool inventoryAvailable;
        private string inventoryStatus = "Нет активного инвентаря";
        private readonly List<ItemEntry> inventoryItems = new List<ItemEntry>();
        private readonly Dictionary<string, int> itemCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private int totalStacks;
        private int hexStacks;
        private Recommendation bestCommon;
        private Recommendation bestUncommon;
        private Recommendation bestOverall;


        private SkillInfo primaryInfo;
        private SkillInfo secondaryInfo;
        private SkillInfo utilityInfo;
        private SkillInfo specialInfo;

        // Read-only on-screen HUD, isolated from the simulation and network.
        private GUIStyle windowStyle;
        private GUIStyle labelStyle;
        private GUIStyle headingStyle;
        private GUIStyle smallStyle;
        private GUIStyle subtleStyle;
        private GUIStyle boldStyle;
        private GUIStyle centeredStyle;
        private GUIStyle pillStyle;
        private GUIStyle buttonStyle;
        private Texture2D solidPixel;
        private readonly IconManager icons = new IconManager();
        private sealed class ItemEntry
        {
            public string Name;
            public string Id;
            public string Tier;
            public int Count;
            public Sprite Icon;
        }

        private sealed class Recommendation
        {
            public string Id;
            public string Name;
            public string Tier;
            public string Reason;
            public string Source;
            public int Count;
            public int Goal;
            public double Score;
            public double MarginalPercent;
            public string StackState;
            public string StackText;
            public string ImpactText;
            public EvidenceStatus Status;
            public Sprite Icon;
        }

        private sealed class SkillInfo
        {
            public string Id;
            public string Name;
            public SkillSnapshot Snapshot;
            public Sprite Icon;
            public int VariantIndex = -1;
            public int DefaultVariantIndex = -1;
            public bool FoundInFamily;
        }

        private sealed class RunTrendPoint
        {
            public int Stage, Overall, Damage, Survival, Clear;
            public float Time;
        }

        private void Awake()
        {
            settings = AdvisorSettings.Bind(Config);
            windowRect.x = settings.WindowX.Value;
            windowRect.y = settings.WindowY.Value;
            playStyle = settings.RiskStyle.Value;
            buildGoal = settings.BuildGoal.Value;
            automaticMode = settings.AutomaticCommand.Value;
            commandMode = settings.ManualCommand.Value;
            iconOnly = settings.WindowMode.Value == 0;
            expanded = settings.WindowMode.Value == 2;
            deathHistoryPath = System.IO.Path.Combine(Paths.ConfigPath, "SanaBuildOverlay-history.tsv");
            runHistoryPath = System.IO.Path.Combine(Paths.ConfigPath, "SanaBuildOverlay-runs.tsv");
            LoadDeathHistory();
            GlobalEventManager.onCharacterDeathGlobal += OnCharacterDeathGlobal;
            GlobalEventManager.onServerDamageDealt += OnServerDamageDealt;
            Logger.LogInfo("Sana Build Overlay v0.14.3 started. Fast ground-loot scan and resilient icon refresh enabled.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(settings != null ? settings.ToggleKey.Value : KeyCode.F8))
            {
                visible = !visible;
                if (visible) nextRefreshTime = 0f;
            }

            UpdateCursor();
            if (!visible) return;
            if (!RoR2Application.loadFinished) return;
            if (!catalogValidated) ValidateRuleCatalog();

            bool lowOverhead = settings != null && settings.PerformanceMode.Value;
            LocalUser fastUser = LocalUserManager.GetFirstLocalUser();
            CharacterBody fastBody = fastUser != null ? fastUser.cachedBody : null;
            if (fastBody != null && snapshot != null && Time.unscaledTime >= nextLootRefreshTime)
            {
                nextLootRefreshTime = Time.unscaledTime + (lowOverhead ? 0.30f : 0.20f);
                bool refreshChoices = Time.unscaledTime >= nextChoiceLootRefreshTime;
                if (refreshChoices)
                    nextChoiceLootRefreshTime = Time.unscaledTime + (lowOverhead ? 2f : 1f);
                ReadNearbyLoot(fastBody, refreshChoices);
            }
            if (Time.unscaledTime >= nextIconRefreshTime)
            {
                nextIconRefreshTime = Time.unscaledTime + 0.20f;
                RefreshPriorityIcons();
            }
            if (Time.unscaledTime < nextRefreshTime)
                return;

            nextRefreshTime = Time.unscaledTime + (lowOverhead ? 1.0f : 0.60f);
            try
            {
                LocalUser user = LocalUserManager.GetFirstLocalUser();
                CharacterBody body = user != null ? user.cachedBody : null;

                if (body == null)
                {
                    ObserveMissingBody();
                    ClearCharacter();
                }
                else
                {
                    hasCharacter = true;
                    characterName = Translate(body.baseNameToken, body.baseNameToken);
                    isRailgunner = IsRailgunner(body.baseNameToken, characterName);
                    ReadMode();
                    ReadStats(body);
                    if (!lowOverhead || Time.unscaledTime >= nextSkillRefreshTime)
                    {
                        nextSkillRefreshTime = Time.unscaledTime + 5f;
                        ReadSkills(body.skillLocator);
                    }
                    if (!lowOverhead || Time.unscaledTime >= nextInventoryRefreshTime)
                    {
                        nextInventoryRefreshTime = Time.unscaledTime + 2f;
                        ReadInventory(body.inventory);
                        AnalyzeCoverage();
                    }
                    ReadRunContext(body);
                    long adviceFingerprint = ComputeAdviceFingerprint(body);
                    if (snapshot == null || !lowOverhead || adviceFingerprint != lastAdviceFingerprint)
                    {
                        ReadSnapshot(body);
                        UpdateRecommendations();
                        lastAdviceFingerprint = adviceFingerprint;
                    }
                    if ((!lowOverhead || (overlayInputActive && expanded && page == 5)) && Time.unscaledTime >= nextTeamRefreshTime)
                    {
                        nextTeamRefreshTime = Time.unscaledTime + (lowOverhead ? 5f : 3f);
                        ReadTeam();
                    }
                    ObserveLife(body);
                }
                errorLogged = false;
            }
            catch (Exception ex)
            {
                ClearCharacter();
                characterName = "Ошибка получения данных";
                if (!errorLogged)
                {
                    Logger.LogWarning("Read-only state refresh failed: " + ex);
                    errorLogged = true;
                }
            }
        }

        private void ValidateRuleCatalog()
        {
            catalogValidated = true;
            int itemOk = 0, itemMissing = 0, tierMismatch = 0, equipmentOk = 0, equipmentMissing = 0;
            foreach (ItemRule rule in ItemDatabase.Rules)
            {
                ItemDef def = ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex(rule.Id));
                if (def == null)
                {
                    itemMissing++;
                    Logger.LogWarning("Item rule missing from live catalog: " + rule.Id);
                    continue;
                }
                if (def.tier.ToString() != rule.Tier)
                {
                    tierMismatch++;
                    Logger.LogWarning("Item rule tier mismatch: " + rule.Id + " expected " + rule.Tier
                        + ", live " + def.tier + ", token " + def.nameToken);
                    continue;
                }
                itemOk++;
            }
            foreach (string id in EquipmentAdvisor.KnownIds)
            {
                if (EquipmentCatalog.GetEquipmentDef(EquipmentCatalog.FindEquipmentIndex(id)) == null) equipmentMissing++;
                else equipmentOk++;
            }
            var cursedIds = new List<string>();
            for (int i = 0; i < ItemCatalog.itemCount; i++)
            {
                ItemDef def = ItemCatalog.GetItemDef((ItemIndex)i);
                if (def != null && def.tier.ToString() == "Cursed") cursedIds.Add(def.name + " (" + def.nameToken + ")");
            }
            Logger.LogInfo("Live Cursed catalog: " + string.Join(", ", cursedIds.ToArray()));
            Logger.LogInfo("Rule catalog check: items " + itemOk + " ok, " + itemMissing + " missing, " + tierMismatch
                + " tier mismatch; equipment " + equipmentOk + " ok, " + equipmentMissing + " missing.");
        }

        private void ClearCharacter()
        {
            hasCharacter = false;
            characterName = "Нет активного персонажа";
            level = hp = shield = barrier = damage = critStat = critChance = critDamage = expectedDamage = critMultiplier = attackSpeed = armor = moveSpeed = "—";
            regen = armorEffect = effectiveHealth = oneShotProtection = activeBuffs = activeDebuffs = "—";
            critDisclaimer = "Обычная модель критов";
            portrait = null;
            primaryInfo = secondaryInfo = utilityInfo = specialInfo = null;
            inventoryAvailable = false;
            inventoryStatus = "Нет активного инвентаря";
            inventoryItems.Clear();
            itemCounts.Clear();
            totalStacks = 0;
            hexStacks = 0;
            bestCommon = bestUncommon = bestRare = bestOverall = bestLongTerm = null;
            rankedAdvice.Clear();
            cachedLocalAdvice.Clear();
            lastAdviceFingerprint = long.MinValue;
            snapshot = null;
            buildMetrics = null;
            runReadiness = null;
            advancedAssessment = null;
            decisionReport = null;
            nextStageForecast = "Следующий этап ещё не рассчитан.";
            bestEquipmentAdvice = null;
            currentEquipmentId = "";
            currentEquipmentName = "Пусто";
            recommendedEquipmentName = "Нет проверенного совета";
            recommendedEquipmentReason = "Список Q-снаряжения пока ограничен.";
            recommendedEquipmentIcon = null;
            activeArchetype = null;
            lastArchetypeId = strategyNotice = "";
            strategyNoticeUntil = 0f;
            nearbyOffers.Clear();
            cachedChoiceOffers.Clear();
            cachedEquipmentAdvice.Clear();
            nearbyImpactCache.Clear();
            nearbyImpactFingerprint = long.MinValue;
            bestAvailable = null;
            nearbyStatus = "Нет активного забега";
            nearbyFacilityAdvice = "Рядом нет поддержанного принтера, утилизатора или котла.";
            teamMembers.Clear();
            weakestTeamMember = null;
            teamDistribution = null;
            teamStatus = "Нет активного забега";
            ClearRunContext();
            encounterKind = EncounterKind.Exploration;
            hostileCount = 0;
            encounterSummary = "Исследование";
            dangerRadar = "Опасная цель не определена.";
            isRailgunner = false;
            hasCritStat = false;
            critPercentCurrent = 0f;
            currentDamageRaw = naturalDamageRaw = currentHealthRaw = naturalHealthRaw = double.NaN;
            currentEffectiveHealthRaw = currentMoveSpeedRaw = naturalMoveSpeedRaw = double.NaN;
            nextItemText = "Рекомендации появятся после начала забега";
            nextItemReason = "";
        }

        private void ReadStats(CharacterBody body)
        {
            currentDamageRaw = naturalDamageRaw = currentHealthRaw = naturalHealthRaw = double.NaN;
            currentEffectiveHealthRaw = currentMoveSpeedRaw = naturalMoveSpeedRaw = double.NaN;
            level = Number(body, "level", "0");
            float damageValue;
            float critValue;
            float attackSpeedValue;
            float armorValue;
            float moveSpeedValue;

            bool hasDamage = TryNumber(body, "damage", out damageValue);
            bool hasCrit = TryNumber(body, "crit", out critValue);
            hasCritStat = hasCrit;
            critPercentCurrent = critValue;
            bool hasAttackSpeed = TryNumber(body, "attackSpeed", out attackSpeedValue);
            bool hasArmor = TryNumber(body, "armor", out armorValue);
            bool hasMoveSpeed = TryNumber(body, "moveSpeed", out moveSpeedValue);

            if (hasDamage) currentDamageRaw = damageValue;
            if (hasMoveSpeed) currentMoveSpeedRaw = moveSpeedValue;
            float levelValue, baseValue, perLevelValue;
            if (!TryNumber(body, "level", out levelValue) || levelValue < 1f) levelValue = 1f;
            if (TryNumber(body, "baseDamage", out baseValue) && TryNumber(body, "levelDamage", out perLevelValue))
                naturalDamageRaw = baseValue + perLevelValue * (levelValue - 1f);
            if (TryNumber(body, "baseMaxHealth", out baseValue) && TryNumber(body, "levelMaxHealth", out perLevelValue))
                naturalHealthRaw = baseValue + perLevelValue * (levelValue - 1f);
            if (TryNumber(body, "baseMoveSpeed", out baseValue)) naturalMoveSpeedRaw = baseValue;

            damage = hasDamage ? FormatNumber(damageValue, "0.##") : "—";
            critStat = hasCrit ? FormatPercent(critValue) : "—";
            attackSpeed = hasAttackSpeed ? FormatNumber(attackSpeedValue, "0.##") : "—";
            armor = hasArmor ? FormatNumber(armorValue, "0.##") : "—";
            moveSpeed = hasMoveSpeed ? FormatNumber(moveSpeedValue, "0.##") : "—";
            regen = Number(body, "regen", "0.##") + (TryNumber(body, "regen", out moveSpeedValue) ? " HP/с" : "");
            portrait = ReadMember(body, "portraitIcon") as Texture;

            object healthComponent = ReadMember(body, "healthComponent");
            hp = Ratio(healthComponent, "health", "fullHealth");
            if (hp == "—")
                hp = Ratio(body, "health", "maxHealth");
            shield = Ratio(healthComponent, "shield", "fullShield");
            barrier = Number(healthComponent, "barrier", "0.#");
            float fullCombined;
            float currentBarrier;
            bool hasFullCombined = TryNumber(healthComponent, "fullCombinedHealth", out fullCombined);
            if (hasFullCombined) currentHealthRaw = fullCombined;
            if (hasArmor && hasFullCombined)
            {
                if (!TryNumber(healthComponent, "barrier", out currentBarrier)) currentBarrier = 0f;
                double multiplier = StatCalculator.ArmorDamageMultiplier(armorValue);
                double reduction = (1d - multiplier) * 100d;
                armorEffect = (reduction >= 0 ? "−" : "+") + Math.Abs(reduction).ToString("0.#", CultureInfo.CurrentCulture) + "% обычного урона";
                currentEffectiveHealthRaw = StatCalculator.EffectiveHealth(fullCombined + Math.Max(0f, currentBarrier), armorValue);
                effectiveHealth = FormatNumber((float)currentEffectiveHealthRaw, "0.#");
            }
            else armorEffect = effectiveHealth = "—";
            bool osp;
            float ospFraction;
            object ospValue = ReadMember(body, "hasOneShotProtection");
            osp = ospValue is bool && (bool)ospValue;
            if (TryNumber(body, "oneShotProtectionFraction", out ospFraction))
                oneShotProtection = osp ? "активна • порог " + FormatPercent(ospFraction * 100f) : "отключена";
            else oneShotProtection = osp ? "активна" : "—";
            ReadBuffSummary(body);

            float critChanceValue = Mathf.Clamp(critValue, 0f, 100f) / 100f;
            float critMultiplierValue = float.NaN;
            float reflectedCritMultiplier;
            if (TryNumber(body, "critMultiplier", out reflectedCritMultiplier) && reflectedCritMultiplier > 0f)
                critMultiplierValue = reflectedCritMultiplier;

            float critDamageValue = damageValue * critMultiplierValue;
            float expectedDamageValue = damageValue * (1f - critChanceValue) + critDamageValue * critChanceValue;

            critChance = FormatPercent(critChanceValue * 100f);
            critMultiplier = critMultiplierValue.ToString("0.##", CultureInfo.CurrentCulture) + "x";
            critDamage = FormatNumber(critDamageValue, "0.##");
            expectedDamage = FormatNumber(expectedDamageValue, "0.##");

            if (!hasDamage || !hasCrit || float.IsNaN(critMultiplierValue))
            {
                critChance = "—";
                critMultiplier = "—";
                critDamage = "—";
                expectedDamage = "—";
                critDisclaimer = "Недостаточно данных для расчёта критического урона.";
            }
            else if (isRailgunner)
            {
                critChance = "не применяется";
                critDamage = "—";
                expectedDamage = "—";
                critMultiplier = "особая механика";
                critDisclaimer = "Railgunner: шанс крита преобразуется в урон критических попаданий. Универсальная формула ожидаемого урона тут неверна, поэтому значение не вычисляется.";
            }
            else
            {
                critDisclaimer = "Модель попадания с коэффициентом 100%: без удачи, гарантированных критов и особых пассивок. Это не фактический урон навыка или DPS. Множитель прочитан из игры.";
            }
        }

        private void ReadBuffSummary(CharacterBody body)
        {
            int buffTypes = 0, buffStacks = 0, debuffTypes = 0, debuffStacks = 0;
            try
            {
                for (int i = 0; i < BuffCatalog.buffCount; i++)
                {
                    BuffIndex index = (BuffIndex)i;
                    int count = body.GetBuffCount(index);
                    if (count <= 0) continue;
                    BuffDef def = BuffCatalog.GetBuffDef(index);
                    if (def != null && def.isDebuff) { debuffTypes++; debuffStacks += count; }
                    else { buffTypes++; buffStacks += count; }
                }
                activeBuffs = buffTypes + " типов • " + buffStacks + " стаков";
                activeDebuffs = debuffTypes + " типов • " + debuffStacks + " стаков";
            }
            catch
            {
                activeBuffs = activeDebuffs = "Неизвестно";
            }
        }

        private static bool IsRailgunner(string token, string localized)
        {
            return (!string.IsNullOrEmpty(token) && token.IndexOf("RAILGUNNER", StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrEmpty(localized) && (localized.IndexOf("Railgunner", StringComparison.OrdinalIgnoreCase) >= 0
                || localized.IndexOf("Рельсотрон", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        // Only reads the local CharacterBody's inventory. No GiveItem, RemoveItem or networking calls.
        // Iterate the live catalog so DLC/other mod items can be displayed without guessed IDs.
        private void ReadInventory(Inventory inventory)
        {
            inventoryItems.Clear();
            itemCounts.Clear();
            totalStacks = 0;
            hexStacks = 0;
            currentEquipmentId = "";
            currentEquipmentName = "Пусто";
            inventoryAvailable = false;
            inventoryStatus = "Инвентарь недоступен";
            if (inventory == null)
                return;

            ReadEquipment(inventory);

            int n = ItemCatalog.itemCount;
            if (n <= 0)
            {
                inventoryStatus = "Каталог предметов ещё не загружен";
                return;
            }

            long fingerprint = 1469598103934665603L;
            for (int i = 0; i < n; i++)
            {
                ItemIndex index = (ItemIndex)i;
                int count = inventory.GetItemCountEffective(index);
                if (count <= 0)
                    continue;
                unchecked
                {
                    fingerprint ^= i;
                    fingerprint *= 1099511628211L;
                    fingerprint ^= count;
                    fingerprint *= 1099511628211L;
                }

                ItemDef def = ItemCatalog.GetItemDef(index);
                string id = def != null ? def.name : "UnknownIndex#" + i.ToString(CultureInfo.InvariantCulture);
                string label = def != null ? Translate(def.nameToken, id) : id;
                string tier = def != null ? def.tier.ToString() : "Unknown";
                if (string.IsNullOrEmpty(id))
                    id = "UnknownIndex#" + i.ToString(CultureInfo.InvariantCulture);
                itemCounts[id] = count;
                totalStacks += count;
                if (tier == "Hex") hexStacks += count;
                inventoryItems.Add(new ItemEntry { Name = label, Id = id, Tier = tier, Count = count });
            }
            inventoryItems.Sort(delegate(ItemEntry a, ItemEntry b)
            {
                int byTier = TierOrder(a.Tier).CompareTo(TierOrder(b.Tier));
                return byTier != 0 ? byTier : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            unchecked
            {
                fingerprint ^= currentEquipmentId == null ? 0 : currentEquipmentId.GetHashCode();
                fingerprint *= 1099511628211L;
            }
            inventoryFingerprint = fingerprint;
            inventoryAvailable = true;
            inventoryStatus = inventoryItems.Count == 0 ? "Инвентарь пуст" :
                "Предметов: " + inventoryItems.Count + " видов, " + totalStacks + " стаков"
                    + (hexStacks > 0 ? " • хэксов: " + hexStacks : "");
        }

        private static int TierOrder(string tier)
        {
            switch (tier)
            {
                case "Tier1": return 10;
                case "Tier2": return 20;
                case "Tier3": return 30;
                case "Boss": return 40;
                case "Lunar": return 50;
                case "VoidTier1": return 60;
                case "VoidTier2": return 61;
                case "VoidTier3": return 62;
                case "Hex": return 70;
                case "Cursed": return 71;
                case "Page": return 72;
                case "FoodTier": return 73;
                case "AssignedAtRuntime": return 74;
                case "NoTier": return 99;
                default: return 90;
            }
        }

        private long ComputeAdviceFingerprint(CharacterBody body)
        {
            unchecked
            {
                long hash = inventoryFingerprint;
                hash = hash * 31 + knownStagesCompleted;
                hash = hash * 31 + (int)encounterKind;
                hash = hash * 31 + (commandMode ? 1 : 0);
                hash = hash * 31 + playStyle;
                hash = hash * 31 + buildGoal;
                hash = hash * 31 + (body == null || body.baseNameToken == null ? 0 : body.baseNameToken.GetHashCode());
                float bodyLevel;
                hash = hash * 31 + (body != null && TryNumber(body, "level", out bodyLevel) ? Mathf.FloorToInt(bodyLevel) : 0);
                return hash;
            }
        }

        private void ReadEquipment(Inventory inventory)
        {
            currentEquipmentId = "";
            currentEquipmentName = "Пусто";
            EquipmentIndex index = inventory.currentEquipmentIndex;
            EquipmentDef def = EquipmentCatalog.GetEquipmentDef(index);
            if (def == null) return;
            currentEquipmentId = def.name ?? "";
            currentEquipmentName = Translate(def.nameToken, currentEquipmentId);
        }

        private void ClearRunContext()
        {
            stageCompleted = runTime = difficultyName = difficultyCoefficient = partyCount = currentMoney = "—";
            partyEvidence = "Недоступно";
            remoteVisibility = "Не проверено";
            activeArtifacts = "—";
            knownPartyCount = 0;
            knownStagesCompleted = -1;
            knownDifficultyCoefficient = -1f;
            interactableBudget = approximateShare = "—";
            mobilityCoverage = defenseCoverage = offenseCoverage = supportCoverage = "Нет данных";
            nextStageWarning = "Недостаточно данных для прогноза";
        }

        // Only reads values exposed by the installed RoR2 build. Some custom game modes
        // expose a different Run subclass or no equivalent of these properties.
        private void ReadRunContext(CharacterBody body)
        {
            ClearRunContext();
            object run = ReadStaticMember(typeof(Run), "instance");
            if (run == null)
                return;

            float n;
            if (TryNumber(run, "stageClearCount", out n) && n >= 0f)
            {
                knownStagesCompleted = (int)n;
                stageCompleted = knownStagesCompleted.ToString(CultureInfo.CurrentCulture);
            }
            if (TryNumber(run, "difficultyCoefficient", out n) && n > 0f)
            {
                knownDifficultyCoefficient = n;
                difficultyCoefficient = n.ToString("0.##", CultureInfo.CurrentCulture) + "x";
            }

            object difficulty = ReadMember(run, "selectedDifficulty");
            if (difficulty != null)
                difficultyName = difficulty.ToString();

            // GetRunStopwatch is the game clock, not Time.time (which includes menus).
            object seconds = CallPublicNoArg(run, "GetRunStopwatch");
            float clock;
            if (TryConvertNumber(seconds, out clock) && clock >= 0f)
            {
                int total = Mathf.FloorToInt(clock);
                runTime = (total / 60).ToString(CultureInfo.InvariantCulture)
                    + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture);
            }

            if (TryNumber(run, "participatingPlayerCount", out n) && n >= 1f && n <= 32f)
            {
                knownPartyCount = (int)n;
                partyCount = knownPartyCount.ToString(CultureInfo.CurrentCulture);
                partyEvidence = "Участники, по данным Run";
            }
            // Do not substitute NetworkUser count silently: it may include spectators
            // or differ from the number counted by the game for difficulty scaling.

            object master = ReadMember(body, "master");
            if (TryNumber(master, "money", out n))
                currentMoney = n.ToString("0", CultureInfo.CurrentCulture);

            bool lowOverhead = settings != null && settings.PerformanceMode.Value;
            if (!lowOverhead || (overlayInputActive && expanded && page == 5)) ProbeRemoteInventory(body);
            else remoteVisibility = "Открой вкладку КОМАНДА и удерживай Alt для обновления";
            if (!lowOverhead || (overlayInputActive && expanded && page == 4)) ReadActiveArtifacts();
            if ((!lowOverhead || (overlayInputActive && expanded && (page == 3 || page == 4 || page == 5)))
                && Time.unscaledTime >= nextThreatRefreshTime)
            {
                nextThreatRefreshTime = Time.unscaledTime + (lowOverhead ? 3f : 1.5f);
                ReadEncounterContext(body);
            }

            if (knownPartyCount > 0)
            {
                // Standard Scene Director interactable credits: +50% per additional player.
                // This is NOT a prediction of number of chests or actual available items.
                float multiplier = 1f + 0.5f * (knownPartyCount - 1);
                interactableBudget = multiplier.ToString("0.##", CultureInfo.CurrentCulture) + "x";
                approximateShare = (multiplier / knownPartyCount * 100f)
                    .ToString("0.#", CultureInfo.CurrentCulture) + "% от соло-бюджета";
            }
        }

        private void ReadActiveArtifacts()
        {
            RunArtifactManager manager = RunArtifactManager.instance;
            if (manager == null) { activeArtifacts = "Недоступно"; return; }
            var names = new List<string>();
            try
            {
                for (int i = 0; i < ArtifactCatalog.artifactCount; i++)
                {
                    ArtifactDef def = ArtifactCatalog.GetArtifactDef((ArtifactIndex)i);
                    if (def != null && manager.IsArtifactEnabled(def))
                        names.Add(Translate(def.nameToken, def.cachedName));
                }
                activeArtifacts = names.Count == 0 ? "Нет" : string.Join(", ", names.ToArray());
            }
            catch { activeArtifacts = "Неизвестно"; }
        }

        private void ReadEncounterContext(CharacterBody localBody)
        {
            hostileCount = 0;
            bool bossPresent = false;
            double bestThreat = double.MinValue;
            string threatName = "";
            float threatDistance = 0f;
            try
            {
                IEnumerable bodies = ReadStaticMember(typeof(CharacterBody), "readOnlyInstancesList") as IEnumerable;
                if (bodies != null)
                {
                    foreach (object candidate in bodies)
                    {
                        if (candidate == null) continue;
                        object team = ReadMember(candidate, "teamComponent");
                        object teamIndex = ReadMember(team, "teamIndex");
                        if (teamIndex == null || teamIndex.ToString().IndexOf("Monster", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        object hc = ReadMember(candidate, "healthComponent");
                        object alive = ReadMember(hc, "alive");
                        if (alive is bool && !(bool)alive) continue;
                        hostileCount++;
                        object champion = ReadMember(candidate, "isChampion");
                        if (champion is bool && (bool)champion) bossPresent = true;
                        CharacterBody enemy = candidate as CharacterBody;
                        if (enemy != null && localBody != null)
                        {
                            float distance = Vector3.Distance(localBody.corePosition, enemy.corePosition);
                            float fullHealth;
                            object enemyHealth = ReadMember(enemy, "healthComponent");
                            if (!TryNumber(enemyHealth, "fullCombinedHealth", out fullHealth)) fullHealth = 1f;
                            double threat = fullHealth / Math.Max(8f, distance) * (champion is bool && (bool)champion ? 4d : 1d);
                            if (threat > bestThreat)
                            {
                                bestThreat = threat;
                                threatName = Translate(enemy.baseNameToken, enemy.baseNameToken);
                                threatDistance = distance;
                            }
                        }
                    }
                }
            }
            catch { hostileCount = 0; }

            bool teleporterActive = false;
            object teleporter = ReadStaticMember(typeof(TeleporterInteraction), "instance");
            if (teleporter != null)
            {
                object charging = ReadMember(teleporter, "isCharging");
                teleporterActive = charging is bool && (bool)charging;
                object state = ReadMember(teleporter, "activationState");
                if (state != null && state.ToString().IndexOf("Charg", StringComparison.OrdinalIgnoreCase) >= 0
                    && state.ToString().IndexOf("Idle", StringComparison.OrdinalIgnoreCase) < 0) teleporterActive = true;
            }
            encounterKind = bossPresent ? EncounterKind.Boss : teleporterActive ? EncounterKind.Teleporter
                : hostileCount >= 12 ? EncounterKind.Swarm : EncounterKind.Exploration;
            encounterSummary = encounterKind == EncounterKind.Boss ? "Босс"
                : encounterKind == EncounterKind.Teleporter ? "Телепортер"
                : encounterKind == EncounterKind.Swarm ? "Толпа • врагов " + hostileCount
                : "Исследование" + (hostileCount > 0 ? " • врагов " + hostileCount : "");
            dangerRadar = string.IsNullOrEmpty(threatName) ? "Опасная цель не определена."
                : "Главная видимая угроза: " + threatName + " • " + threatDistance.ToString("0", CultureInfo.CurrentCulture) + " м.";
        }

        private void LoadDeathHistory()
        {
            try
            {
                if (string.IsNullOrEmpty(deathHistoryPath) || !File.Exists(deathHistoryPath)) return;
                string[] lines = File.ReadAllLines(deathHistoryPath, Encoding.UTF8);
                deathReviewCount = lines.Length;
                if (lines.Length > 0)
                {
                    string[] cells = lines[lines.Length - 1].Split('\t');
                    lastDeathReview = cells.Length >= 4 ? cells[1] + " • этап " + cells[2] + " • " + cells[3] : lines[lines.Length - 1];
                }
            }
            catch (Exception ex) { Logger.LogWarning("Death history read failed: " + ex.GetType().Name); }
        }

        private void ObserveLife(CharacterBody body)
        {
            if (body == null) return;
            object hc = ReadMember(body, "healthComponent");
            object aliveValue = ReadMember(hc, "alive");
            bool alive = !(aliveValue is bool) || (bool)aliveValue;
            float current, full;
            if (TryNumber(hc, "combinedHealth", out current) && TryNumber(hc, "fullCombinedHealth", out full) && full > 0f)
                lastObservedHealthFraction = current / full;
            if (alive) observedAlive = true;
            else if (observedAlive)
            {
                RecordDeathReview();
                observedAlive = false;
                lastObservedHealthFraction = 0f;
            }
        }

        private void ObserveMissingBody()
        {
            if (observedAlive && lastObservedHealthFraction <= 0.01f) RecordDeathReview();
            observedAlive = false;
            lastObservedHealthFraction = 1f;
        }

        private void OnCharacterDeathGlobal(DamageReport report)
        {
            try
            {
                LocalUser user = LocalUserManager.GetFirstLocalUser();
                CharacterBody local = user == null ? null : user.cachedBody;
                if (report == null || local == null || report.victimBody != local) return;
                string attacker = report.attackerBody == null ? "неизвестный источник"
                    : Translate(report.attackerBody.baseNameToken, report.attackerBody.baseNameToken);
                lastFatalHit = "Последний удар: " + attacker + " • " + report.damageInfo.damage.ToString("0.#", CultureInfo.CurrentCulture)
                    + " урона • " + report.damageInfo.damageType + ".";
                if (observedAlive) { RecordDeathReview(); observedAlive = false; }
            }
            catch (Exception ex) { Logger.LogWarning("Death report capture failed: " + ex.GetType().Name); }
        }

        private void RecordDeathReview()
        {
            string review = AdvancedAdvisor.DeathReview(runReadiness, advancedAssessment);
            if (!string.IsNullOrEmpty(lastFatalHit)) review += " " + lastFatalHit;
            lastDeathReview = characterName + " • этап " + Math.Max(1, knownStagesCompleted + 1) + " • " + review;
            runReport = "Забег завершён: " + review;
            deathReviewCount++;
            try
            {
                string cleanName = (characterName ?? "Неизвестно").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
                string cleanReview = review.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
                string line = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\t" + cleanName + "\t"
                    + Math.Max(1, knownStagesCompleted + 1).ToString(CultureInfo.InvariantCulture) + "\t" + cleanReview + Environment.NewLine;
                File.AppendAllText(deathHistoryPath, line, Encoding.UTF8);
                var trend = new StringBuilder();
                foreach (RunTrendPoint point in runTrend)
                {
                    if (trend.Length > 0) trend.Append(';');
                    trend.Append(point.Stage).Append(':').Append(point.Overall).Append(':')
                        .Append(point.Damage).Append(':').Append(point.Survival).Append(':').Append(point.Clear);
                }
                string runLine = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\t" + cleanName + "\t"
                    + cleanReview + "\t" + trend + Environment.NewLine;
                File.AppendAllText(runHistoryPath, runLine, Encoding.UTF8);
            }
            catch (Exception ex) { Logger.LogWarning("Death history write failed: " + ex.GetType().Name); }
            lastFatalHit = "";
        }

        // Presence check only. Seeing an Inventory component on this client does NOT
        // prove that its content is fully synchronized, especially with unmodded peers.
        private void ProbeRemoteInventory(CharacterBody localBody)
        {
            object users = ReadStaticMember(typeof(NetworkUser), "readOnlyInstancesList");
            IEnumerable list = users as IEnumerable;
            if (list == null)
            {
                remoteVisibility = "Список NetworkUser не прочитан";
                return;
            }
            int seenUsers = 0;
            int seenBodies = 0;
            int seenInventoryObjects = 0;
            try
            {
                foreach (object networkUser in list)
                {
                    if (networkUser == null) continue;
                    seenUsers++;
                    object body = CallPublicNoArg(networkUser, "GetCurrentBody");
                    if (body == null || object.ReferenceEquals(body, localBody)) continue;
                    seenBodies++;
                    if (ReadMember(body, "inventory") is Inventory)
                        seenInventoryObjects++;
                }
                remoteVisibility = "NetworkUser " + seenUsers + "; удалённых тел " + seenBodies
                    + "; объектов Inventory " + seenInventoryObjects
                    + ". Синхронизация стаков не подтверждена.";
            }
            catch
            {
                remoteVisibility = "Ошибка чтения списка NetworkUser";
            }
        }

        private static object ReadStaticMember(Type type, string name)
        {
            try
            {
                FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null) return field.GetValue(null);
                PropertyInfo prop = type.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null && prop.CanRead && prop.GetIndexParameters().Length == 0)
                    return prop.GetValue(null, null);
            }
            catch { }
            return null;
        }

        private static object CallPublicNoArg(object source, string methodName)
        {
            if (source == null) return null;
            try
            {
                MethodInfo m = source.GetType().GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                if (m != null) return m.Invoke(source, null);
            }
            catch { }
            return null;
        }

        private static bool TryConvertNumber(object value, out float result)
        {
            result = 0f;
            if (value == null) return false;
            try
            {
                result = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                return !float.IsNaN(result) && !float.IsInfinity(result);
            }
            catch { return false; }
        }

        // Coverage is evidence about a small *verified* item subset, not an
        // assertion about actual DPS, readiness percent or chance of surviving.
        private void AnalyzeCoverage()
        {
            if (!inventoryAvailable)
                return;
            bool movement = HasOwnedCategory("movement");
            bool defense = HasOwnedCategory("defense");
            bool offense = HasOwnedCategory("offense");
            bool healing = HasOwnedCategory("healing");

            mobilityCoverage = movement ? "Есть предмет из проверенного набора" : "Нет Hoof / Feather (другие эффекты не оценены)";
            defenseCoverage = defense ? "Есть Tougher Times / Safer Spaces" : "Нет Bear / Safer Spaces (не оценены другие защиты)";
            offenseCoverage = offense ? "Есть предмет из проверенного набора" : "Нет подтверждённых усилений из малого набора";
            supportCoverage = healing ? "Есть лечение из проверенного набора" : "Нет лечения из поддержанного набора";

            if (!defense)
                nextStageWarning = "Рекомендация: рассмотреть защиту. Нет Bear/Safer Spaces; фактическая живучесть может быть выше за счёт других эффектов и навыков.";
            else if (!movement)
                nextStageWarning = "Рекомендация: рассмотреть мобильность. Hoof/Feather не найдены; активные навыки и другие предметы не учтены.";
            else if (!offense)
                nextStageWarning = "Рекомендация: проверить урон по боссам. Эта версия пока не вычисляет реальный DPS.";
            else
                nextStageWarning = "Найдено базовое покрытие трёх категорий, но готовность к следующему этапу не подтверждена.";
            if (hexStacks > 0)
                nextStageWarning += " Активных хэксов: " + hexStacks + ". Их штрафы зависят от типа; совет — проверить очищение в Hallowed Shrine.";
        }

        private bool HasOwnedCategory(string category)
        {
            foreach (ItemRule rule in ItemDatabase.Rules)
                if (rule.Category == category && CountOf(rule.Id) > 0) return true;
            return false;
        }

        private int CountOf(string internalId)
        {
            int count;
            return itemCounts.TryGetValue(internalId, out count) ? count : 0;
        }

        private void ReadMode()
        {
            if (!automaticMode) { modeEvidence = "Ручной выбор"; return; }
            var manager = RunArtifactManager.instance;
            if (manager == null || RoR2Content.Artifacts.Command == null)
            { commandMode = false; modeEvidence = "Артефакт пока не прочитан"; return; }
            commandMode = manager.IsArtifactEnabled(RoR2Content.Artifacts.Command);
            modeEvidence = "Авто: артефакт " + (commandMode ? "включён" : "выключен");
        }

        private void ReadSnapshot(CharacterBody body)
        {
            snapshot = new BuildSnapshot();
            snapshot.InventoryKnown = inventoryAvailable;
            snapshot.Railgunner = isRailgunner;
            snapshot.CharacterToken = body.baseNameToken ?? "";
            snapshot.Crit = hasCritStat ? critPercentCurrent : double.NaN;
            float value;
            snapshot.Damage = TryNumber(body, "damage", out value) ? value : double.NaN;
            snapshot.CritMultiplier = TryNumber(body, "critMultiplier", out value) ? value : double.NaN;
            PopulateAdvancedSnapshot(body, snapshot);
            snapshot.Primary = primaryInfo == null ? null : primaryInfo.Snapshot;
            snapshot.Secondary = secondaryInfo == null ? null : secondaryInfo.Snapshot;
            snapshot.Utility = utilityInfo == null ? null : utilityInfo.Snapshot;
            snapshot.Special = specialInfo == null ? null : specialInfo.Snapshot;
            foreach (var pair in itemCounts) snapshot.Counts[pair.Key] = pair.Value;
            for (int i = 0; i < ItemCatalog.itemCount; i++)
            {
                ItemIndex index = (ItemIndex)i;
                ItemDef def = ItemCatalog.GetItemDef(index);
                if (def == null || def.hidden || string.IsNullOrEmpty(def.name)) continue;
                ProfileLiveItem(snapshot, def, snapshot.Count(def.name));
                if (Run.instance != null && Run.instance.availableItems.Contains(index)) snapshot.Available.Add(def.name);
            }
        }

        private static void ProfileLiveItem(BuildSnapshot build, ItemDef def, int count)
        {
            if (build == null || def == null || string.IsNullOrEmpty(def.name)) return;
            string category = InferLiveCategory(def);
            build.Categories[def.name] = category;
            if (IsExactRule(def.name) || count <= 0) return;
            int weight = Math.Min(5, count);
            if (category == "offense") build.LiveDamageItems += weight;
            else if (category == "healing" || category == "defense") build.LiveHealingItems += weight;
            else build.LiveUtilityItems += weight;
            if (IsLiveAoe(def)) build.LiveAoeItems += weight;
        }

        private static bool IsExactRule(string id)
        {
            foreach (ItemRule rule in ItemDatabase.Rules)
                if (string.Equals(rule.Id, id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string InferLiveCategory(ItemDef def)
        {
            string cached;
            if (def != null && !string.IsNullOrEmpty(def.name) && liveCategoryCache.TryGetValue(def.name, out cached)) return cached;
            string tags = LiveTags(def);
            string category = tags.IndexOf("Damage", StringComparison.OrdinalIgnoreCase) >= 0 ? "offense"
                : tags.IndexOf("Healing", StringComparison.OrdinalIgnoreCase) >= 0 ? "healing"
                : tags.IndexOf("Sprint", StringComparison.OrdinalIgnoreCase) >= 0 ? "movement" : "utility";
            if (def != null && !string.IsNullOrEmpty(def.name)) liveCategoryCache[def.name] = category;
            return category;
        }

        private static bool IsLiveAoe(ItemDef def)
        {
            string id = def == null ? "" : def.name ?? "";
            bool cached;
            if (!string.IsNullOrEmpty(id) && liveAoeCache.TryGetValue(id, out cached)) return cached;
            string tags = LiveTags(def);
            bool aoe = tags.IndexOf("OnKillEffect", StringComparison.OrdinalIgnoreCase) >= 0
                || id == "IgniteOnKill" || id == "ChainLightningVoid" || id == "BounceNearby"
                || id == "BleedOnHitAndExplode" || id == "MoreMissile" || id == "ElementalRingVoid";
            if (!string.IsNullOrEmpty(id)) liveAoeCache[id] = aoe;
            return aoe;
        }

        private static string LiveTags(ItemDef def)
        {
            if (def == null) return "";
            string cached;
            if (!string.IsNullOrEmpty(def.name) && liveTagCache.TryGetValue(def.name, out cached)) return cached;
            try
            {
                Array tags = ReadMember(def, "tags") as Array;
                if (tags == null) return "";
                var names = new List<string>();
                foreach (object tag in tags) if (tag != null) names.Add(tag.ToString());
                string value = string.Join(",", names.ToArray());
                if (!string.IsNullOrEmpty(def.name)) liveTagCache[def.name] = value;
                return value;
            }
            catch { return ""; }
        }

        private List<ItemAdvice> EvaluateAllItems(BuildSnapshot build, RunReadiness readiness,
            AdvancedAssessment assessment, int impactLimit)
        {
            List<ItemAdvice> result = RecommendationEngine.Evaluate(build);
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ItemAdvice item in result) known.Add(item.Id);
            foreach (string id in build.Available)
            {
                if (known.Contains(id)) continue;
                ItemDef def = ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex(id));
                if (def == null || def.hidden) continue;
                string tier = def.tier.ToString();
                if (tier == "NoTier" || tier == "Hex" || id.IndexOf("Scrap", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                string category;
                if (!build.Categories.TryGetValue(id, out category)) category = InferLiveCategory(def);
                double score = tier == "Tier1" ? 52d : tier == "Tier2" ? 62d : tier == "Tier3" ? 74d
                    : tier == "Boss" || tier == "VoidBoss" ? 70d : 48d;
                if (category == "offense" && build.DamageReadiness >= 0) score += Math.Max(0, 100 - build.DamageReadiness) * 0.45d;
                if ((category == "defense" || category == "healing") && build.DefenseReadiness >= 0)
                    score += Math.Max(0, 105 - Math.Min(build.HealthReadiness < 0 ? 105 : build.HealthReadiness, build.DefenseReadiness)) * 0.65d;
                if (category == "movement" && build.MobilityReadiness >= 0) score += Math.Max(0, 105 - build.MobilityReadiness) * 0.60d;
                if (IsLiveAoe(def) && assessment != null && assessment.Clear.Percent >= 0)
                    score += Math.Max(0, 105 - assessment.Clear.Percent) * 0.55d;
                string tags = LiveTags(def);
                string reason = "Live-профиль установленной игры: категория " + category
                    + (string.IsNullOrEmpty(tags) ? "." : "; теги " + tags + ".")
                    + " Точная формула эффекта ещё не моделируется, поэтому балл консервативный.";
                if (tier == "Lunar" || tier.IndexOf("Void", StringComparison.OrdinalIgnoreCase) >= 0)
                    reason += " Перед выбором проверь обмен, порчу и отрицательную сторону предмета.";
                result.Add(new ItemAdvice { Id = id, Tier = tier, Count = build.Count(id), Goal = build.Count(id) + 1,
                    Score = score, Reason = reason, Status = EvidenceStatus.Estimated, StackState = "LIVE-ПРОФИЛЬ" });
            }
            result.Sort(delegate(ItemAdvice a, ItemAdvice b) { int c = b.Score.CompareTo(a.Score); return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id); });
            // What-if analysis is the expensive part. Only the strongest visible
            // candidates need it during the regular refresh; nearby choices are
            // simulated once when the loot scanner actually sees them.
            int limit = Math.Min(Math.Max(0, impactLimit), result.Count);
            for (int i = 0; i < limit; i++)
                result[i].ImpactText = DecisionAdvisor.Simulate(build, result[i].Id, readiness, assessment).Summary;
            return result;
        }

        private void UpdateRecommendations()
        {
            bestCommon = bestUncommon = bestRare = bestOverall = bestLongTerm = null;
            rankedAdvice.Clear();
            if (snapshot == null || !inventoryAvailable)
            { nextItemText = "Нет данных для рекомендации"; nextItemReason = inventoryStatus; return; }
            snapshot.Style = playStyle;
            snapshot.Goal = buildGoal;
            buildMetrics = BuildProfileAnalyzer.Measure(snapshot);
            runReadiness = ReadinessAnalyzer.Evaluate(knownStagesCompleted, knownDifficultyCoefficient,
                currentDamageRaw, naturalDamageRaw, currentHealthRaw, naturalHealthRaw,
                currentEffectiveHealthRaw, currentMoveSpeedRaw, naturalMoveSpeedRaw, buildMetrics);
            RunReadiness next = ReadinessAnalyzer.Evaluate(knownStagesCompleted + 1, knownDifficultyCoefficient,
                currentDamageRaw, naturalDamageRaw, currentHealthRaw, naturalHealthRaw,
                currentEffectiveHealthRaw, currentMoveSpeedRaw, naturalMoveSpeedRaw, buildMetrics);
            int nextMinimum = MinReadiness(next);
            nextStageForecast = nextMinimum < 0 ? "Следующий этап: недостаточно данных."
                : nextMinimum < 70 ? "Следующий этап: опасно • минимум " + nextMinimum + "%"
                : nextMinimum < 95 ? "Следующий этап: просадка • минимум " + nextMinimum + "%"
                : "Следующий этап: база выглядит готовой • минимум " + nextMinimum + "%";
            ApplyReadiness(snapshot, runReadiness);
            advancedAssessment = AdvancedAdvisor.Evaluate(snapshot, buildMetrics, runReadiness);
            effectiveRiskStyle = AdaptiveRiskStyle(runReadiness);
            snapshot.Style = effectiveRiskStyle;
            List<ArchetypeMatch> archetypes = ArchetypeMatcher.Match(snapshot);
            activeArchetype = null;
            foreach (ArchetypeMatch candidate in archetypes)
            {
                if (candidate.Verified || settings != null && settings.ExperimentalAdvice.Value)
                { activeArchetype = candidate; break; }
            }
            string detectedId = activeArchetype == null ? "" : activeArchetype.Id;
            if (!string.IsNullOrEmpty(detectedId) && detectedId != lastArchetypeId && settings != null && settings.SynergyNotifications.Value)
            {
                strategyNotice = "Новая синергия: " + activeArchetype.Name + (activeArchetype.Verified ? "" : " • гипотеза");
                strategyNoticeUntil = Time.unscaledTime + 8f;
            }
            lastArchetypeId = detectedId;
            List<ItemAdvice> evaluated = EvaluateAllItems(snapshot, runReadiness, advancedAssessment, 12);
            cachedLocalAdvice.Clear();
            cachedLocalAdvice.AddRange(evaluated);
            decisionReport = DecisionAdvisor.Analyze(snapshot, buildMetrics, runReadiness, advancedAssessment, evaluated);
            if (decisionReport != null && !string.IsNullOrEmpty(decisionReport.Notification)
                && decisionReport.Notification != lastDecisionNotice && settings != null && settings.SynergyNotifications.Value)
            {
                strategyNotice = decisionReport.Notification;
                strategyNoticeUntil = Time.unscaledTime + 7f;
            }
            lastDecisionNotice = decisionReport == null ? "" : decisionReport.Notification;
            CaptureRunTrend();
            ItemAdvice practical = RecommendationEngine.SelectPractical(evaluated);
            foreach (ItemAdvice advice in evaluated)
            {
                ItemDef def = ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex(advice.Id));
                if (def == null) continue;
                var rec = new Recommendation { Id = advice.Id, Name = Translate(def.nameToken, advice.Id),
                    Tier = advice.Tier, Count = advice.Count, Goal = advice.Goal, Score = advice.Score,
                    Status = advice.Status, Reason = advice.Reason, ImpactText = advice.ImpactText,
                    MarginalPercent = advice.MarginalPercent, StackState = advice.StackState, StackText = advice.StackText,
                    Source = "Описание установленной игры; приоритет — эвристика" };
                rankedAdvice.Add(rec);
                if (bestLongTerm == null) bestLongTerm = rec;
                if (practical != null && advice.Id == practical.Id) bestOverall = rec;
                if (rec.Tier == "Tier1" && bestCommon == null) bestCommon = rec;
                if (rec.Tier == "Tier2" && bestUncommon == null) bestUncommon = rec;
                if (rec.Tier == "Tier3" && bestRare == null) bestRare = rec;
            }
            // Without reading a real pickup/shop/Command choice, the compact HUD
            // must prefer an obtainable common goal. Higher tiers stay in the plan.
            if (bestOverall == null) bestOverall = bestCommon ?? bestUncommon ?? bestRare;
            UpdateEquipmentAdvice();
            nextItemText = bestOverall == null ? "Нет совета в проверенном наборе" : bestOverall.Name;
            nextItemReason = bestOverall == null ? "Это не означает, что билд завершён." : bestOverall.Reason
                + (bestLongTerm != null && bestLongTerm != bestOverall
                    ? " Долгосрочная цель: " + bestLongTerm.Name + "." : "");
        }

        private void UpdateEquipmentAdvice()
        {
            bestEquipmentAdvice = null;
            cachedEquipmentAdvice.Clear();
            recommendedEquipmentName = "Нет проверенного совета";
            recommendedEquipmentReason = "Список Q-снаряжения пока ограничен.";
            recommendedEquipmentIcon = null;
            Run run = Run.instance;
            if (run == null) return;
            var equipment = new EquipmentSnapshot { CurrentId = currentEquipmentId,
                CharacterToken = snapshot == null ? "" : snapshot.CharacterToken,
                Style = effectiveRiskStyle, Goal = buildGoal, CommandMode = commandMode };
            foreach (string id in EquipmentAdvisor.KnownIds)
            {
                EquipmentIndex index = EquipmentCatalog.FindEquipmentIndex(id);
                EquipmentDef def = EquipmentCatalog.GetEquipmentDef(index);
                if (def == null || !def.canDrop || def.isLunar || def.isBoss) continue;
                if (!run.availableEquipment.Contains(index)) continue;
                equipment.Available.Add(id);
            }
            cachedEquipmentAdvice.AddRange(EquipmentAdvisor.EvaluateAll(equipment));
            bestEquipmentAdvice = EquipmentAdvisor.Evaluate(equipment);
            if (bestEquipmentAdvice == null) return;
            EquipmentDef target = EquipmentCatalog.GetEquipmentDef(EquipmentCatalog.FindEquipmentIndex(bestEquipmentAdvice.Id));
            if (target == null) return;
            recommendedEquipmentName = Translate(target.nameToken, bestEquipmentAdvice.Id);
            recommendedEquipmentReason = (bestEquipmentAdvice.AlreadyEquipped ? "Оставить текущее. " : "Если встретишь — рассмотреть замену. ")
                + bestEquipmentAdvice.Reason;
            recommendedEquipmentIcon = icons.GetEquipment(bestEquipmentAdvice.Id, target);
        }

        private void ReadNearbyLoot(CharacterBody body, bool refreshChoices)
        {
            nearbyOffers.Clear();
            bestAvailable = null;
            if (body == null || snapshot == null) { nearbyStatus = "Нет активного персонажа"; return; }
            try
            {
                IList<ItemAdvice> itemAdvice = cachedLocalAdvice;
                long localMoney = body.master == null ? -1L : body.master.money;
                List<NearbyOffer> worldOffers = NearbyLootReader.Read(body.corePosition, itemAdvice,
                    cachedEquipmentAdvice, 70f, localMoney, true, false);
                if (refreshChoices)
                {
                    cachedChoiceOffers.Clear();
                    cachedChoiceOffers.AddRange(NearbyLootReader.Read(body.corePosition, itemAdvice,
                        cachedEquipmentAdvice, 70f, localMoney, false, true));
                }

                var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < worldOffers.Count; i++)
                    AddMergedNearbyOffer(worldOffers[i], body.corePosition, merged);
                for (int i = 0; i < cachedChoiceOffers.Count; i++)
                    AddMergedNearbyOffer(cachedChoiceOffers[i], body.corePosition, merged);
                NearbyLootReader.Sort(nearbyOffers);

                if (nearbyImpactFingerprint != lastAdviceFingerprint)
                {
                    nearbyImpactFingerprint = lastAdviceFingerprint;
                    nearbyImpactCache.Clear();
                }
                // Preserve an exact delta for every choice the player can really
                // reach. Cache it until the inventory/stage fingerprint changes;
                // recalculating the same what-if four times per second caused stalls.
                for (int i = 0; i < nearbyOffers.Count; i++)
                {
                    NearbyOffer offer = nearbyOffers[i];
                    if (offer != null && !offer.IsEquipment && string.IsNullOrEmpty(offer.ImpactText))
                    {
                        string impact;
                        if (!nearbyImpactCache.TryGetValue(offer.Id, out impact))
                        {
                            impact = DecisionAdvisor.Simulate(snapshot, offer.Id, runReadiness, advancedAssessment).Summary;
                            nearbyImpactCache[offer.Id] = impact;
                        }
                        offer.ImpactText = impact;
                    }
                }
                ReadNearbyFacilities(body.corePosition);
                RefreshNearbyIcons();
                for (int i = 0; i < nearbyOffers.Count; i++)
                {
                    if (nearbyOffers[i].Status != EvidenceStatus.Unknown &&
                        (!nearbyOffers[i].AffordabilityKnown || nearbyOffers[i].Affordable)) { bestAvailable = nearbyOffers[i]; break; }
                }
                if (bestAvailable == null)
                    for (int i = 0; i < nearbyOffers.Count; i++)
                        if (nearbyOffers[i].Status != EvidenceStatus.Unknown) { bestAvailable = nearbyOffers[i]; break; }
                nearbyStatus = nearbyOffers.Count == 0 ? "В радиусе 70 м не найден видимый лут" :
                    "Найдено рядом: " + nearbyOffers.Count + ". Оцениваются только уже раскрытые клиенту варианты.";
            }
            catch (Exception ex)
            {
                nearbyStatus = "Чтение лута недоступно: " + ex.GetType().Name;
            }
        }

        private void AddMergedNearbyOffer(NearbyOffer offer, Vector3 origin, HashSet<string> seen)
        {
            if (offer == null) return;
            offer.Distance = Vector3.Distance(origin, offer.Position);
            if (offer.Distance > 70f) return;
            string key = offer.Id + "|" + Mathf.RoundToInt(offer.Position.x * 2f) + "|"
                + Mathf.RoundToInt(offer.Position.y * 2f) + "|" + Mathf.RoundToInt(offer.Position.z * 2f);
            if (seen.Add(key)) nearbyOffers.Add(offer);
        }

        private void RefreshNearbyIcons()
        {
            for (int i = 0; i < nearbyOffers.Count; i++)
            {
                NearbyOffer offer = nearbyOffers[i];
                RefreshOfferIcon(offer);
            }
        }

        private void RefreshOfferIcon(NearbyOffer offer)
        {
            if (offer == null || string.IsNullOrEmpty(offer.Id)) return;
            Sprite loaded;
            if (offer.IsEquipment)
            {
                EquipmentDef def = EquipmentCatalog.GetEquipmentDef(EquipmentCatalog.FindEquipmentIndex(offer.Id));
                loaded = icons.GetEquipment(offer.Id, def);
            }
            else
            {
                ItemDef def = ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex(offer.Id));
                loaded = icons.Get(offer.Id, def);
            }
            // Keep PickupDef's immediate icon while Addressables is loading,
            // then replace it with the icon that belongs to the current ID.
            if (loaded != null) offer.Icon = loaded;
        }

        private void RefreshPriorityIcons()
        {
            // The HUD target is refreshed first and independently from the heavy
            // recommendation rebuild, so an async DLC icon can appear immediately.
            RefreshOfferIcon(bestAvailable);
            RefreshRecommendationIcon(bestOverall);
            RefreshRecommendationIcon(bestCommon);
            RefreshRecommendationIcon(bestUncommon);
            RefreshRecommendationIcon(bestRare);
            RefreshRecommendationIcon(bestLongTerm);

            if (bestEquipmentAdvice != null)
            {
                EquipmentDef equipment = EquipmentCatalog.GetEquipmentDef(
                    EquipmentCatalog.FindEquipmentIndex(bestEquipmentAdvice.Id));
                Sprite loaded = icons.GetEquipment(bestEquipmentAdvice.Id, equipment);
                if (loaded != null) recommendedEquipmentIcon = loaded;
            }

            // Round-robin the rest with a small fixed budget. This avoids a large
            // Addressables burst after opening the inventory while still healing
            // icons whose underlying scene asset was unloaded.
            int total = inventoryItems.Count + rankedAdvice.Count;
            if (total <= 0) return;
            for (int step = 0; step < 6; step++)
            {
                int slot = iconRefreshCursor++ % total;
                if (slot < inventoryItems.Count)
                {
                    ItemEntry item = inventoryItems[slot];
                    ItemDef def = ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex(item.Id));
                    Sprite loaded = icons.Get(item.Id, def);
                    if (loaded != null) item.Icon = loaded;
                }
                else
                    RefreshRecommendationIcon(rankedAdvice[slot - inventoryItems.Count]);
            }
        }

        private void RefreshRecommendationIcon(Recommendation recommendation)
        {
            if (recommendation == null || string.IsNullOrEmpty(recommendation.Id)) return;
            ItemDef def = ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex(recommendation.Id));
            Sprite loaded = icons.Get(recommendation.Id, def);
            if (loaded != null) recommendation.Icon = loaded;
        }

        private void ReadNearbyFacilities(Vector3 origin)
        {
            if (Time.unscaledTime < nextFacilityRefreshTime) return;
            nextFacilityRefreshTime = Time.unscaledTime + (settings != null && settings.PerformanceMode.Value ? 8f : 3f);
            nearbyFacilityAdvice = "Рядом нет поддержанного принтера, утилизатора или котла.";
            try
            {
                PurchaseInteraction[] interactions = UnityEngine.Object.FindObjectsOfType<PurchaseInteraction>();
                string printer = null, scrapper = null, cauldron = null;
                foreach (PurchaseInteraction interaction in interactions)
                {
                    if (interaction == null || Vector3.Distance(origin, interaction.transform.position) > 70f) continue;
                    string n = interaction.gameObject == null ? "" : interaction.gameObject.name ?? "";
                    if (n.IndexOf("Scrapper", StringComparison.OrdinalIgnoreCase) >= 0)
                        scrapper = decisionReport == null ? "проверь лишние стаки" : decisionReport.Scrap;
                    else if (n.IndexOf("Duplicator", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Printer", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        NearbyOffer offer = nearbyOffers.Find(delegate(NearbyOffer x) { return x != null && x.Source == "3D-принтер"; });
                        printer = offer == null ? "выход принтера ещё скрыт" : offer.Name + " • "
                            + (string.IsNullOrEmpty(offer.ImpactText) ? "сравни с текущими дефицитами" : offer.ImpactText);
                    }
                    else if (n.IndexOf("Cauldron", StringComparison.OrdinalIgnoreCase) >= 0)
                        cauldron = "не отдавай единственный источник слабой категории";
                }
                var parts = new List<string>();
                if (printer != null) parts.Add("ПРИНТЕР: " + printer);
                if (scrapper != null) parts.Add("УТИЛИЗАТОР: " + scrapper);
                if (cauldron != null) parts.Add("КОТЁЛ: " + cauldron);
                if (parts.Count > 0) nearbyFacilityAdvice = string.Join("  |  ", parts.ToArray());
            }
            catch { nearbyFacilityAdvice = "Проверка ближайших обменов недоступна."; }
        }

        private int AdaptiveRiskStyle(RunReadiness readiness)
        {
            if (settings == null || !settings.AdaptiveRisk.Value || readiness == null) return playStyle;
            int survival = int.MaxValue;
            if (readiness.Health.Percent >= 0) survival = Math.Min(survival, readiness.Health.Percent);
            if (readiness.Defense.Percent >= 0) survival = Math.Min(survival, readiness.Defense.Percent);
            if (advancedAssessment != null && advancedAssessment.Burst.Percent >= 0) survival = Math.Min(survival, advancedAssessment.Burst.Percent);
            if (survival < 80) return 0;
            int lowest = survival;
            if (readiness.Damage.Percent >= 0) lowest = Math.Min(lowest, readiness.Damage.Percent);
            if (readiness.Mobility.Percent >= 0) lowest = Math.Min(lowest, readiness.Mobility.Percent);
            return lowest >= 120 ? 2 : 1;
        }

        private void CaptureRunTrend()
        {
            object run = Run.instance;
            if (run == null || runReadiness == null || advancedAssessment == null) return;
            if (!object.ReferenceEquals(run, trendRunIdentity))
            {
                trendRunIdentity = run;
                runTrend.Clear();
                trackedDamage = biggestTrackedHit = 0d;
                trackedHits = 0;
                nextTrendCapture = 0f;
                runReport = "Новый забег: собираю историю полос.";
            }
            int stage = Math.Max(1, knownStagesCompleted + 1);
            bool stageChanged = runTrend.Count == 0 || runTrend[runTrend.Count - 1].Stage != stage;
            if (!stageChanged && Time.unscaledTime < nextTrendCapture) return;
            nextTrendCapture = Time.unscaledTime + 20f;
            int damageValue = AverageKnown(runReadiness.Damage.Percent, advancedAssessment.Opener.Percent, advancedAssessment.Tempo.Percent);
            int survivalValue = AverageKnown(runReadiness.Health.Percent, runReadiness.Defense.Percent,
                advancedAssessment.Burst.Percent, advancedAssessment.Sustain.Percent);
            int clearValue = advancedAssessment.Clear.Percent;
            int overall = AverageKnown(damageValue, survivalValue, clearValue, runReadiness.Mobility.Percent);
            runTrend.Add(new RunTrendPoint { Stage = stage, Time = Time.unscaledTime, Overall = overall,
                Damage = damageValue, Survival = survivalValue, Clear = clearValue });
            if (runTrend.Count > 60) runTrend.RemoveAt(0);
            string weak = clearValue <= damageValue && clearValue <= survivalValue ? "зачистка " + clearValue + "%"
                : survivalValue <= damageValue ? "живучесть " + survivalValue + "%" : "урон " + damageValue + "%";
            runReport = "Этап " + stage + " • общий темп " + overall + "% • слабое: " + weak
                + " • точек истории " + runTrend.Count
                + (trackedHits > 0 ? " • отслежено на хосте урона " + trackedDamage.ToString("0", CultureInfo.CurrentCulture)
                    + " • макс. удар " + biggestTrackedHit.ToString("0", CultureInfo.CurrentCulture) : "") + ".";
        }

        private static int AverageKnown(params int[] values)
        {
            int sum = 0, count = 0;
            foreach (int value in values) if (value >= 0) { sum += value; count++; }
            return count == 0 ? -1 : (int)Math.Round((double)sum / count);
        }

        private static int MinReadiness(RunReadiness r)
        {
            if (r == null) return -1;
            int value = int.MaxValue;
            int[] all = { r.Damage.Percent, r.Health.Percent, r.Defense.Percent, r.Mobility.Percent };
            foreach (int n in all) if (n >= 0) value = Math.Min(value, n);
            return value == int.MaxValue ? -1 : value;
        }

        private string BuildTeamGapSummary()
        {
            int damage = 0, survival = 0, clear = 0, mobility = 0, count = 0;
            foreach (TeamMemberSnapshot member in teamMembers)
            {
                if (member == null || !member.Alive || member.Readiness == null) continue;
                damage += Math.Max(0, member.Readiness.Damage.Percent);
                mobility += Math.Max(0, member.Readiness.Mobility.Percent);
                survival += AverageKnown(member.Readiness.Health.Percent, member.Readiness.Defense.Percent,
                    member.Advanced == null ? -1 : member.Advanced.Burst.Percent,
                    member.Advanced == null ? -1 : member.Advanced.Sustain.Percent);
                clear += member.Advanced == null ? 0 : Math.Max(0, member.Advanced.Clear.Percent);
                count++;
            }
            if (count == 0) return "Командный пробел не рассчитан.";
            damage /= count; mobility /= count; survival /= count; clear /= count;
            int minimum = Math.Min(Math.Min(damage, mobility), Math.Min(survival, clear));
            string area = minimum == clear ? "зачистка" : minimum == survival ? "живучесть"
                : minimum == mobility ? "мобильность" : "урон";
            return "Главный пробел команды: " + area + " " + minimum + "%";
        }

        private void OnServerDamageDealt(DamageReport report)
        {
            try
            {
                if (settings != null && settings.PerformanceMode.Value && (!overlayInputActive || !expanded || page != 4)) return;
                LocalUser user = LocalUserManager.GetFirstLocalUser();
                CharacterBody local = user == null ? null : user.cachedBody;
                if (report == null || local == null || report.attackerBody != local) return;
                double value = report.damageInfo.damage;
                if (value <= 0d) return;
                trackedDamage += value;
                trackedHits++;
                biggestTrackedHit = Math.Max(biggestTrackedHit, value);
            }
            catch { }
        }

        private void ReadTeam()
        {
            teamMembers.Clear();
            weakestTeamMember = null;
            teamDistribution = null;
            try
            {
                IList<NetworkUser> users = NetworkUser.readOnlyInstancesList;
                if (users == null) { teamStatus = "NetworkUser недоступен"; return; }
                foreach (NetworkUser user in users)
                {
                    if (user == null) continue;
                    CharacterBody body = user.GetCurrentBody();
                    CharacterMaster master = user.master;
                    Inventory inventory = body != null ? body.inventory : master != null ? master.inventory : null;
                    var member = new TeamMemberSnapshot();
                    member.UserName = !string.IsNullOrEmpty(user.userName) ? user.userName : "Игрок";
                    member.IsLocal = user.localUser != null;
                    member.BodyVisible = body != null;
                    member.InventoryVisible = inventory != null;
                    member.Alive = body != null && body.healthComponent != null && body.healthComponent.alive;
                    member.CharacterName = body != null ? Translate(body.baseNameToken, body.baseNameToken) : "Тело недоступно";
                    if (inventory != null)
                    {
                        int total = 0;
                        for (int i = 0; i < ItemCatalog.itemCount; i++) total += Math.Max(0, inventory.GetItemCountEffective((ItemIndex)i));
                        member.TotalStacks = total;
                        member.Build = BuildTeamSnapshot(body, inventory);
                        member.Metrics = BuildProfileAnalyzer.Measure(member.Build);
                        member.Readiness = ReadBodyReadiness(body, member.Metrics);
                        ApplyReadiness(member.Build, member.Readiness);
                        member.Advanced = AdvancedAdvisor.Evaluate(member.Build, member.Metrics, member.Readiness);
                        TeamAnalyzer.Summarize(member);
                        // Team bars need the best recommendation, not a complete
                        // what-if report for every catalog entry.
                        List<ItemAdvice> advice = EvaluateAllItems(member.Build, member.Readiness, member.Advanced, 0);
                        member.Role = DecisionAdvisor.TeamRole(member.Build, member.Metrics, member.Advanced);
                        member.BestAdvice = RecommendationEngine.SelectPractical(advice);
                    }
                    teamMembers.Add(member);
                }
                weakestTeamMember = TeamAnalyzer.FindWeakest(teamMembers);
                teamGapSummary = BuildTeamGapSummary();
                string itemForTeam = bestAvailable != null && !bestAvailable.IsEquipment ? bestAvailable.Id : bestOverall == null ? null : bestOverall.Id;
                teamDistribution = TeamAnalyzer.ChooseRecipient(itemForTeam, teamMembers);
                teamStatus = "NetworkUser: " + users.Count + "; снимков: " + teamMembers.Count
                    + ". Inventory означает видимый объект, не доказанную полноту синхронизации.";
            }
            catch (Exception ex)
            {
                teamStatus = "Ошибка чтения команды: " + ex.GetType().Name;
            }
        }

        private BuildSnapshot BuildTeamSnapshot(CharacterBody body, Inventory inventory)
        {
            var b = new BuildSnapshot { InventoryKnown = inventory != null, Style = effectiveRiskStyle, Goal = buildGoal };
            if (body != null)
            {
                b.CharacterToken = body.baseNameToken ?? "";
                b.Railgunner = IsRailgunner(body.baseNameToken, "");
                float n;
                b.Crit = TryNumber(body, "crit", out n) ? n : double.NaN;
                b.Damage = TryNumber(body, "damage", out n) ? n : double.NaN;
                b.CritMultiplier = TryNumber(body, "critMultiplier", out n) ? n : double.NaN;
                PopulateAdvancedSnapshot(body, b);
                SkillLocator locator = body.skillLocator;
                b.Primary = SnapshotSkill(locator == null ? null : locator.primary);
                b.Secondary = SnapshotSkill(locator == null ? null : locator.secondary);
                b.Utility = SnapshotSkill(locator == null ? null : locator.utility);
                b.Special = SnapshotSkill(locator == null ? null : locator.special);
            }
            if (inventory != null)
            {
                for (int i = 0; i < ItemCatalog.itemCount; i++)
                {
                    ItemIndex index = (ItemIndex)i;
                    ItemDef def = ItemCatalog.GetItemDef(index);
                    if (def == null || string.IsNullOrEmpty(def.name)) continue;
                    int count = inventory.GetItemCountEffective(index);
                    if (count > 0) b.Counts[def.name] = count;
                    ProfileLiveItem(b, def, count);
                }
            }
            Run run = Run.instance;
            if (run != null)
            {
                for (int i = 0; i < ItemCatalog.itemCount; i++)
                {
                    ItemIndex index = (ItemIndex)i;
                    ItemDef def = ItemCatalog.GetItemDef(index);
                    if (def != null && !def.hidden && !string.IsNullOrEmpty(def.name) && run.availableItems.Contains(index)) b.Available.Add(def.name);
                }
            }
            return b;
        }

        private void PopulateAdvancedSnapshot(CharacterBody body, BuildSnapshot b)
        {
            if (body == null || b == null) return;
            float n, levelValue, baseValue, levelValueGain;
            b.NaturalDamage = TryNumber(body, "baseDamage", out baseValue) && TryNumber(body, "levelDamage", out levelValueGain)
                ? baseValue + levelValueGain * ((TryNumber(body, "level", out levelValue) ? Math.Max(1f, levelValue) : 1f) - 1f) : double.NaN;
            b.AttackSpeed = TryNumber(body, "attackSpeed", out n) ? n : double.NaN;
            b.Armor = TryNumber(body, "armor", out n) ? n : double.NaN;
            b.Regen = TryNumber(body, "regen", out n) ? n : double.NaN;
            b.MoveSpeed = TryNumber(body, "moveSpeed", out n) ? n : double.NaN;
            object hc = ReadMember(body, "healthComponent");
            b.Health = TryNumber(hc, "fullCombinedHealth", out n) ? n : double.NaN;
            if (TryNumber(body, "baseMaxHealth", out baseValue) && TryNumber(body, "levelMaxHealth", out levelValueGain))
                b.NaturalHealth = baseValue + levelValueGain * ((TryNumber(body, "level", out levelValue) ? Math.Max(1f, levelValue) : 1f) - 1f);
            if (!double.IsNaN(b.Health) && !double.IsNaN(b.Armor)) b.EffectiveHealth = StatCalculator.EffectiveHealth(b.Health, b.Armor);
            b.Stage = Math.Max(1, knownStagesCompleted + 1);
            b.Difficulty = knownDifficultyCoefficient > 0f ? knownDifficultyCoefficient : double.NaN;
            b.Encounter = encounterKind;
            b.HostileCount = hostileCount;
        }

        private static void ApplyReadiness(BuildSnapshot build, RunReadiness readiness)
        {
            if (build == null || readiness == null) return;
            build.DamageReadiness = readiness.Damage == null ? -1 : readiness.Damage.Percent;
            build.HealthReadiness = readiness.Health == null ? -1 : readiness.Health.Percent;
            build.DefenseReadiness = readiness.Defense == null ? -1 : readiness.Defense.Percent;
            build.MobilityReadiness = readiness.Mobility == null ? -1 : readiness.Mobility.Percent;
        }

        private RunReadiness ReadBodyReadiness(CharacterBody body, BuildMetrics metrics)
        {
            if (body == null)
                return ReadinessAnalyzer.Evaluate(knownStagesCompleted, knownDifficultyCoefficient,
                    double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, metrics);
            float levelValue, damageValue, baseDamage, levelDamage, healthValue, baseHealth, levelHealth;
            float armorValue, barrierValue, moveValue, baseMove;
            levelDamage = levelHealth = 0f;
            if (!TryNumber(body, "level", out levelValue) || levelValue < 1f) levelValue = 1f;
            object healthComponent = ReadMember(body, "healthComponent");
            bool hasDamage = TryNumber(body, "damage", out damageValue);
            bool hasNaturalDamage = TryNumber(body, "baseDamage", out baseDamage) && TryNumber(body, "levelDamage", out levelDamage);
            bool hasHealth = TryNumber(healthComponent, "fullCombinedHealth", out healthValue);
            bool hasNaturalHealth = TryNumber(body, "baseMaxHealth", out baseHealth) && TryNumber(body, "levelMaxHealth", out levelHealth);
            bool hasArmor = TryNumber(body, "armor", out armorValue);
            if (!TryNumber(healthComponent, "barrier", out barrierValue)) barrierValue = 0f;
            double ehp = hasHealth && hasArmor ? StatCalculator.EffectiveHealth(healthValue + Math.Max(0f, barrierValue), armorValue) : double.NaN;
            bool hasMove = TryNumber(body, "moveSpeed", out moveValue);
            bool hasBaseMove = TryNumber(body, "baseMoveSpeed", out baseMove);
            return ReadinessAnalyzer.Evaluate(knownStagesCompleted, knownDifficultyCoefficient,
                hasDamage ? damageValue : double.NaN,
                hasNaturalDamage ? baseDamage + levelDamage * (levelValue - 1f) : double.NaN,
                hasHealth ? healthValue : double.NaN,
                hasNaturalHealth ? baseHealth + levelHealth * (levelValue - 1f) : double.NaN,
                ehp, hasMove ? moveValue : double.NaN, hasBaseMove ? baseMove : double.NaN, metrics);
        }

        private static void AddTeamCount(BuildSnapshot b, Inventory inventory, string id)
        {
            ItemIndex index = ItemCatalog.FindItemIndex(id);
            if (index == ItemIndex.None) return;
            int count = inventory.GetItemCountEffective(index);
            if (count > 0) b.Counts[id] = count;
        }

        private static SkillSnapshot SnapshotSkill(GenericSkill slot)
        {
            if (slot == null || slot.skillDef == null) return null;
            SkillDef def = slot.skillDef;
            return new SkillSnapshot { Id = !string.IsNullOrEmpty(def.skillNameToken) ? def.skillNameToken : def.skillName,
                Name = Translate(def.skillNameToken, def.skillName), Recharge = def.baseRechargeInterval,
                RequiredStock = def.requiredStock, AttackSpeedRestocks = def.attackSpeedBuffsRestockSpeed };
        }

        private static string DisplayTier(string tier)
        {
            if (tier == "Tier1") return "белый";
            if (tier == "Tier2") return "зелёный";
            if (tier == "Tier3") return "красный";
            if (tier == "VoidTier1" || tier == "VoidTier2" || tier == "VoidTier3" || tier == "VoidBoss") return "Пустота";
            if (tier == "Hex") return "хэкс";
            if (tier == "Cursed") return "освящённый";
            if (tier == "Page") return "страница";
            if (tier == "FoodTier") return "еда";
            return tier;
        }

        private static string RecommendationText(Recommendation r)
        {
            if (r == null) return "Пока нет проверенного совета для этой редкости";
            return r.Name + "  x" + r.Count + " → x" + r.Goal;
        }

        private void ReadSkills(SkillLocator locator)
        {
            if (locator == null)
            {
                primaryInfo = secondaryInfo = utilityInfo = specialInfo = null;
                return;
            }
            primaryInfo = ReadSkill(locator.primary);
            secondaryInfo = ReadSkill(locator.secondary);
            utilityInfo = ReadSkill(locator.utility);
            specialInfo = ReadSkill(locator.special);
        }

        private static SkillInfo ReadSkill(GenericSkill slot)
        {
            if (slot == null)
                return null;

            SkillDef active = slot.skillDef;
            if (active == null)
                return new SkillInfo { Name = "Не определён" };

            SkillInfo info = new SkillInfo();
            info.Id = !string.IsNullOrEmpty(active.skillNameToken) ? active.skillNameToken : active.skillName;
            info.Name = Translate(active.skillNameToken, active.skillName);
            info.Snapshot = new SkillSnapshot { Id = info.Id, Name = info.Name,
                Recharge = active.baseRechargeInterval, RequiredStock = active.requiredStock,
                AttackSpeedRestocks = active.attackSpeedBuffsRestockSpeed };
            info.Icon = ReadMember(active, "icon") as Sprite;

            SkillFamily family = slot.skillFamily;
            if (family != null && family.variants != null)
            {
                info.DefaultVariantIndex = (int)family.defaultVariantIndex;
                for (int i = 0; i < family.variants.Length; i++)
                {
                    if (family.variants[i].skillDef == active)
                    {
                        info.VariantIndex = i;
                        info.FoundInFamily = true;
                        break;
                    }
                }
            }
            return info;
        }

        private static string Translate(string token, string fallback)
        {
            if (!string.IsNullOrEmpty(token))
            {
                string localized = Language.GetString(token);
                if (!string.IsNullOrEmpty(localized) && localized != token)
                    return localized;
            }
            if (!string.IsNullOrEmpty(fallback))
                return fallback;
            return !string.IsNullOrEmpty(token) ? token : "Неизвестно";
        }

        private static string SkillText(SkillInfo info)
        {
            if (info == null)
                return "Слот отсутствует";
            string name = !string.IsNullOrEmpty(info.Name) ? info.Name : "Неизвестно";
            if (info.FoundInFamily)
            {
                if (info.VariantIndex != info.DefaultVariantIndex)
                    return name + " [альтернатива #" + (info.VariantIndex + 1) + "]";
                return name + " [основной]";
            }
            if (!string.IsNullOrEmpty(info.Id))
                return name + " [вариант не подтверждён]";
            return name;
        }

        // Prefer the immediate game icon; modern RoR2 builds may expose only
        // a deferred AssetReference via GetIconReference(). Load it ASYNCHRONOUSLY.
        // Reflection avoids hard-linking Addressables (and survives API changes).
        private Sprite GetItemIcon(string id, ItemDef def)
        {
            return icons.Get(id, def);
        }

        private static object ReadMember(object source, string name)
        {
            if (source == null)
                return null;
            try
            {
                Type t = source.GetType();
                FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public);
                if (f != null)
                    return f.GetValue(source);
                PropertyInfo p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (p != null && p.GetIndexParameters().Length == 0 && p.CanRead)
                    return p.GetValue(source, null);
            }
            catch
            {
            }
            return null;
        }

        private static bool TryNumber(object source, string name, out float result)
        {
            result = 0f;
            object value = ReadMember(source, name);
            if (value == null)
                return false;
            try
            {
                result = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                return !float.IsNaN(result) && !float.IsInfinity(result);
            }
            catch
            {
                return false;
            }
        }

        private static string Number(object source, string name, string format)
        {
            float value;
            return TryNumber(source, name, out value)
                ? value.ToString(format, CultureInfo.CurrentCulture) : "—";
        }

        private static string Percent(object source, string name)
        {
            float value;
            return TryNumber(source, name, out value)
                ? value.ToString("0.##", CultureInfo.CurrentCulture) + "%" : "—";
        }

        private static string Ratio(object source, string currentName, string fullName)
        {
            float current;
            float max;
            if (TryNumber(source, currentName, out current) && TryNumber(source, fullName, out max))
                return current.ToString("0.#", CultureInfo.CurrentCulture) + " / "
                    + max.ToString("0.#", CultureInfo.CurrentCulture);
            return "—";
        }

        private static string FormatNumber(float value, string format)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return "—";
            return value.ToString(format, CultureInfo.CurrentCulture);
        }

        private static string FormatPercent(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return "—";
            return value.ToString("0.##", CultureInfo.CurrentCulture) + "%";
        }

        private void OnDestroy()
        {
            SaveSettings();
            GlobalEventManager.onCharacterDeathGlobal -= OnCharacterDeathGlobal;
            GlobalEventManager.onServerDamageDealt -= OnServerDamageDealt;
            icons.Dispose();
            ReleaseCursor();
            if (solidPixel != null) Destroy(solidPixel);

        }

        private void SaveSettings()
        {
            if (settings == null) return;
            settings.WindowX.Value = windowRect.x;
            settings.WindowY.Value = windowRect.y;
            settings.RiskStyle.Value = playStyle;
            settings.BuildGoal.Value = buildGoal;
            settings.AutomaticCommand.Value = automaticMode;
            settings.ManualCommand.Value = commandMode;
            settings.WindowMode.Value = iconOnly ? 0 : expanded ? 2 : 1;
            Config.Save();
        }

    }
}
