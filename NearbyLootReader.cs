using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace SanaBuildOverlay
{
    public sealed class NearbyOffer
    {
        public string Id, Name, Tier, Source, Reason, ImpactText;
        public float Distance;
        public Vector3 Position;
        public double Score;
        public int Count, Goal;
        public bool IsEquipment, IsTierChoice, CountKnown;
        public string CostText = "";
        public bool AffordabilityKnown, Affordable;
        public EvidenceStatus Status = EvidenceStatus.Unknown;
        public Sprite Icon;
    }

    // Reads only pickup controllers that already exist on this client. It never
    // changes a shop, droplet, Command picker, inventory or network state.
    public static class NearbyLootReader
    {
        public static List<NearbyOffer> Read(Vector3 origin, IList<ItemAdvice> itemAdvice,
            IList<EquipmentAdvice> equipmentAdvice, float maxDistance, long localMoney,
            bool includeWorld, bool includeChoices)
        {
            var result = new List<NearbyOffer>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            // Open Command, option and similar pickers expose the exact client-side
            // choices. This is the expensive scene scan, so the caller runs it less
            // often than the InstanceTracker-backed ground-loot pass.
            if (includeChoices)
            {
                PickupPickerController[] pickers = UnityEngine.Object.FindObjectsOfType<PickupPickerController>();
                if (pickers != null)
                {
                    foreach (PickupPickerController picker in pickers)
                    {
                        if (picker == null || !picker.available || picker.options == null) continue;
                        float distance = Vector3.Distance(origin, picker.transform.position);
                        foreach (PickupPickerController.Option option in picker.options)
                            if (option.available) Add(result, seen, option.pickup.pickupIndex, "открытый выбор", distance, picker.transform.position,
                                itemAdvice, equipmentAdvice, maxDistance);
                    }
                }

                // Boss rewards spend a short time as droplets before becoming a
                // GenericPickupController. Reading both states prevents yellow
                // loot from disappearing during the landing transition.
                PickupDropletController[] droplets = UnityEngine.Object.FindObjectsOfType<PickupDropletController>();
                if (droplets != null)
                {
                    foreach (PickupDropletController droplet in droplets)
                    {
                        if (droplet == null || !droplet.isActiveAndEnabled) continue;
#pragma warning disable 618
                        PickupIndex pickupIndex = droplet.pickupIndex;
#pragma warning restore 618
                        Add(result, seen, pickupIndex, "выпавший предмет",
                            Vector3.Distance(origin, droplet.transform.position), droplet.transform.position,
                            itemAdvice, equipmentAdvice, maxDistance);
                    }
                }
            }

            if (includeWorld)
            {
                List<ShopTerminalBehavior> terminals = InstanceTracker.GetInstancesList<ShopTerminalBehavior>();
                if (terminals != null)
                {
                    foreach (ShopTerminalBehavior terminal in terminals)
                    {
                        if (terminal == null || terminal.Networkhidden || terminal.NetworkhasBeenPurchased || terminal.pickupIndexIsHidden) continue;
                        int before = result.Count;
                        Add(result, seen, terminal.CurrentPickup().pickupIndex, SourceName(terminal.gameObject, true),
                            Vector3.Distance(origin, terminal.transform.position), terminal.transform.position, itemAdvice, equipmentAdvice, maxDistance);
                        if (result.Count > before)
                        {
                            PurchaseInteraction purchase = terminal.GetComponent<PurchaseInteraction>() ?? terminal.GetComponentInParent<PurchaseInteraction>();
                            if (purchase != null)
                            {
                                NearbyOffer offer = result[result.Count - 1];
                                offer.CostText = purchase.cost + " • " + purchase.costType;
                                if (purchase.costType.ToString() == "Money" && localMoney >= 0)
                                {
                                    offer.AffordabilityKnown = true;
                                    offer.Affordable = localMoney >= purchase.cost;
                                }
                            }
                        }
                    }
                }

                List<GenericPickupController> pickups = InstanceTracker.GetInstancesList<GenericPickupController>();
                if (pickups != null)
                {
                    foreach (GenericPickupController pickup in pickups)
                    {
                        if (pickup == null || !pickup.isActiveAndEnabled) continue;
                        Add(result, seen, pickup.pickup.pickupIndex, SourceName(pickup.gameObject, false),
                            Vector3.Distance(origin, pickup.transform.position), pickup.transform.position, itemAdvice, equipmentAdvice, maxDistance);
                    }
                }
            }

            Sort(result);
            return result;
        }

        public static void Sort(List<NearbyOffer> result)
        {
            result.Sort(delegate(NearbyOffer a, NearbyOffer b)
            {
                int availableA = a.AffordabilityKnown ? a.Affordable ? 2 : 0 : 1;
                int availableB = b.AffordabilityKnown ? b.Affordable ? 2 : 0 : 1;
                int affordable = availableB.CompareTo(availableA);
                if (affordable != 0) return affordable;
                int known = b.Status != EvidenceStatus.Unknown ? 1 : 0;
                known -= a.Status != EvidenceStatus.Unknown ? 1 : 0;
                if (known != 0) return known;
                int score = b.Score.CompareTo(a.Score);
                if (score != 0) return score;
                int distance = a.Distance.CompareTo(b.Distance);
                return distance != 0 ? distance : string.CompareOrdinal(a.Id, b.Id);
            });
        }

        private static void Add(List<NearbyOffer> output, HashSet<string> seen, PickupIndex pickupIndex,
            string source, float distance, Vector3 position, IList<ItemAdvice> items, IList<EquipmentAdvice> equipment, float maxDistance)
        {
            if (!pickupIndex.isValid || distance > maxDistance) return;
            PickupDef def = PickupCatalog.GetPickupDef(pickupIndex);
            if (def == null) return;
            string key = source + "|" + pickupIndex.value + "|" + Mathf.RoundToInt(distance * 2f);
            if (!seen.Add(key)) return;

            if (def.itemIndex != ItemIndex.None)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(def.itemIndex);
                // If a real pickup controller exposes the item to this client, it
                // is valid loot even when a DLC marks the catalog definition as
                // hidden. Filtering on ItemDef.hidden lost several boss rewards.
                if (itemDef == null) return;
                ItemAdvice advice = FindItem(items, itemDef.name);
                string itemTier = itemDef.tier.ToString();
                bool rareBossPickup = advice == null && (itemTier == "Boss" || itemTier == "VoidBoss");
                if (rareBossPickup && (source == "предмет на земле" || source == "выпавший предмет"))
                    source = "дроп босса";
                output.Add(new NearbyOffer {
                    Id = itemDef.name, Name = Localize(itemDef.nameToken, itemDef.name), Tier = itemTier,
                    Source = source, Distance = distance, Icon = def.GetIcon(),
                    Position = position, ImpactText = advice == null ? "" : advice.ImpactText,
                    Score = advice == null ? rareBossPickup ? 86d : 0d : advice.Score,
                    Count = advice == null ? 0 : advice.Count, Goal = advice == null ? 0 : advice.Goal,
                    CountKnown = advice != null,
                    Reason = advice == null
                        ? rareBossPickup
                            ? "Редкий босс-предмет реально доступен. Персонального правила пока нет; рекомендация основана на его редкости и отсутствии штрафа за подбор."
                            : "Предмет реально виден рядом, но для него ещё нет проверенного правила оценки."
                        : advice.Reason,
                    Status = advice == null ? rareBossPickup ? EvidenceStatus.Estimated : EvidenceStatus.Unknown : advice.Status
                });
                return;
            }

            if (def.equipmentIndex != EquipmentIndex.None)
            {
                EquipmentDef equipmentDef = EquipmentCatalog.GetEquipmentDef(def.equipmentIndex);
                if (equipmentDef == null) return;
                EquipmentAdvice advice = FindEquipment(equipment, equipmentDef.name);
                output.Add(new NearbyOffer {
                    Id = equipmentDef.name, Name = Localize(equipmentDef.nameToken, equipmentDef.name), Tier = "Equipment",
                    Source = source, Distance = distance, Icon = def.GetIcon(), IsEquipment = true,
                    Position = position,
                    Score = advice == null ? 0d : advice.Score, Reason = advice == null
                        ? "Снаряжение реально видно рядом, но для него ещё нет проверенного правила оценки."
                        : advice.Reason, Status = advice == null ? EvidenceStatus.Unknown : advice.Status
                });
                return;
            }

            string tier = def.itemTier.ToString();
            ItemAdvice tierAdvice = FirstTier(items, tier);
            if (tierAdvice == null) return;
            ItemDef target = ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex(tierAdvice.Id));
            output.Add(new NearbyOffer {
                Id = tierAdvice.Id, Name = target == null ? tierAdvice.Id : Localize(target.nameToken, tierAdvice.Id),
                Tier = tier, Source = "Command-выбор • " + source, Distance = distance,
                Position = position, ImpactText = tierAdvice.ImpactText,
                Icon = target == null ? def.GetIcon() : ItemIcon(target.itemIndex), IsTierChoice = true,
                Score = tierAdvice.Score, Count = tierAdvice.Count, Goal = tierAdvice.Goal,
                CountKnown = true,
                Reason = "Эссенция этого тира реально видна рядом. " + tierAdvice.Reason, Status = tierAdvice.Status
            });
        }

        private static ItemAdvice FindItem(IList<ItemAdvice> advice, string id)
        {
            if (advice == null) return null;
            for (int i = 0; i < advice.Count; i++) if (string.Equals(advice[i].Id, id, StringComparison.OrdinalIgnoreCase)) return advice[i];
            return null;
        }

        private static EquipmentAdvice FindEquipment(IList<EquipmentAdvice> advice, string id)
        {
            if (advice == null) return null;
            for (int i = 0; i < advice.Count; i++) if (string.Equals(advice[i].Id, id, StringComparison.OrdinalIgnoreCase)) return advice[i];
            return null;
        }

        private static ItemAdvice FirstTier(IList<ItemAdvice> advice, string tier)
        {
            if (advice == null) return null;
            for (int i = 0; i < advice.Count; i++) if (advice[i].Tier == tier) return advice[i];
            return null;
        }

        private static string SourceName(GameObject gameObject, bool terminal)
        {
            string n = gameObject == null ? "" : gameObject.name ?? "";
            if (n.IndexOf("Duplicator", StringComparison.OrdinalIgnoreCase) >= 0) return "3D-принтер";
            if (n.IndexOf("MultiShop", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("TripleShop", StringComparison.OrdinalIgnoreCase) >= 0) return "магазин";
            if (n.IndexOf("Scrapper", StringComparison.OrdinalIgnoreCase) >= 0) return "утилизатор";
            if (n.IndexOf("LunarCauldron", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Cauldron", StringComparison.OrdinalIgnoreCase) >= 0) return "лунный котёл";
            return terminal ? "терминал" : "предмет на земле";
        }

        private static string Localize(string token, string fallback)
        {
            string text = !string.IsNullOrEmpty(token) ? Language.GetString(token) : "";
            return string.IsNullOrEmpty(text) || text == token ? fallback : text;
        }

        private static Sprite ItemIcon(ItemIndex index)
        {
            PickupDef def = PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex(index));
            return def == null ? null : def.GetIcon();
        }
    }
}
