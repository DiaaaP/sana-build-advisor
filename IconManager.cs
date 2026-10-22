using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SanaBuildOverlay
{
    internal sealed class IconManager : IDisposable
    {
        private readonly Dictionary<string, AsyncOperationHandle<Sprite>> loads = new Dictionary<string, AsyncOperationHandle<Sprite>>();
        private readonly Dictionary<string, Sprite> resolved = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, float> retryAfter = new Dictionary<string, float>();
        public Sprite Get(string id, ItemDef def)
        {
            try { return Load(id, def); }
            catch (Exception ex)
            {
                retryAfter[id] = Time.unscaledTime + 2f;
                UnityEngine.Debug.LogWarning("Sana icon unavailable: " + id + ": " + ex.Message);
                return null;
            }
        }
        public Sprite GetEquipment(string id, EquipmentDef def)
        {
            string key = "equipment:" + id;
            Sprite cached;
            if (resolved.TryGetValue(key, out cached))
            {
                if (Usable(cached)) return cached;
                resolved.Remove(key);
            }
            try
            {
                if (!RoR2Application.loadFinished || def == null) return null;
                Sprite immediate = ImmediateEquipmentIcon(def);
                if (Usable(immediate))
                {
                    resolved[key] = immediate;
                    return immediate;
                }
                if (!CanRetry(key)) return null;
                AsyncOperationHandle<Sprite> handle;
                if (loads.TryGetValue(key, out handle))
                    return ResolveCompleted(key, handle);
                var reference = def.GetIconReference();
                if (reference == null || !reference.RuntimeKeyIsValid()) return null;
                loads[key] = Addressables.LoadAssetAsync<Sprite>(reference.RuntimeKey);
                return null;
            }
            catch (Exception ex)
            {
                retryAfter[key] = Time.unscaledTime + 2f;
                UnityEngine.Debug.LogWarning("Sana equipment icon unavailable: " + id + ": " + ex.Message);
                return null;
            }
        }
        private Sprite Load(string id, ItemDef def)
        {
            Sprite cached;
            if (resolved.TryGetValue(id, out cached))
            {
                if (Usable(cached)) return cached;
                resolved.Remove(id);
            }
            if (!RoR2Application.loadFinished || def == null) return null;
            Sprite immediate = ImmediateItemIcon(def);
            if (Usable(immediate))
            {
                resolved[id] = immediate;
                return immediate;
            }
            if (!CanRetry(id)) return null;
            AsyncOperationHandle<Sprite> handle;
            if (loads.TryGetValue(id, out handle))
                return ResolveCompleted(id, handle);
            var reference = def.GetIconReference();
            if (reference == null || !reference.RuntimeKeyIsValid()) return null;
            // Own a separate handle: never release or reuse the game's AssetReference operation.
            loads[id] = Addressables.LoadAssetAsync<Sprite>(reference.RuntimeKey);
            return null;
        }

        private Sprite ResolveCompleted(string key, AsyncOperationHandle<Sprite> handle)
        {
            if (!handle.IsValid() || !handle.IsDone) return null;
            if (handle.Status != AsyncOperationStatus.Succeeded || !Usable(handle.Result))
            {
                if (handle.IsValid()) Addressables.Release(handle);
                loads.Remove(key);
                retryAfter[key] = Time.unscaledTime + 2f;
                return null;
            }
            resolved[key] = handle.Result;
            retryAfter.Remove(key);
            return handle.Result;
        }

        private bool CanRetry(string key)
        {
            float time;
            if (!retryAfter.TryGetValue(key, out time)) return true;
            if (Time.unscaledTime < time) return false;
            retryAfter.Remove(key);
            return true;
        }

        private static bool Usable(Sprite sprite)
        {
            return sprite != null && sprite.texture != null;
        }

        private static Sprite ImmediateItemIcon(ItemDef def)
        {
            PickupDef pickup = PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex(def.itemIndex));
            return pickup == null ? null : pickup.GetIcon();
        }

        private static Sprite ImmediateEquipmentIcon(EquipmentDef def)
        {
            PickupDef pickup = PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex(def.equipmentIndex));
            return pickup == null ? null : pickup.GetIcon();
        }

        public void Dispose()
        {
            foreach (var handle in loads.Values) if (handle.IsValid()) Addressables.Release(handle);
            loads.Clear();
            resolved.Clear();
            retryAfter.Clear();
        }
    }
}
