using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VsQuest.Systems.Performance;

namespace VsQuest.Harmony.Items
{
    /// <summary>
    /// Wearable stats patches - handles stat calculations from equipped items.
    /// Optimized with caching and batching.
    /// </summary>
    public class WearableStatsPatches
    {
        private static readonly bool EnableTemporalStabilityWearablePatch = true;

        [HarmonyPatch(typeof(ModSystemWearableStats), "onFootStep")]
        public class ModSystemWearableStats_onFootStep_Patch
        {
            public static bool Prefix(EntityPlayer entity)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_ModSystemWearableStats_onFootStep)) return true;
                var stats = WearableStatsCache.GetCachedStats(entity);
                return stats == null || stats.Stealth <= 0f;
            }
        }

        [HarmonyPatch(typeof(EntityBehaviorHealth), "OnFallToGround")]
        public class EntityBehaviorHealth_OnFallToGround_Patch
        {
            public static void Prefix(EntityBehaviorHealth __instance, ref float __state)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_EntityBehaviorHealth_OnFallToGround)) return;
                if (__instance?.entity is not EntityPlayer player) return;

                var stats = WearableStatsCache.GetCachedStats(player);
                if (stats == null || stats.FallDamageReduction == 0f) return;

                __state = __instance.entity.Properties.FallDamageMultiplier;
                float mult = GameMath.Clamp(1f - stats.FallDamageReduction, 0f, 2f);
                __instance.entity.Properties.FallDamageMultiplier = __state * mult;
            }

            public static void Postfix(EntityBehaviorHealth __instance, float __state)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_EntityBehaviorHealth_OnFallToGround)) return;
                if (__instance?.entity == null) return;
                if (__state <= 0f) return;

                __instance.entity.Properties.FallDamageMultiplier = __state;
            }
        }

        [HarmonyPatch(typeof(EntityBehaviorTemporalStabilityAffected), "OnGameTick")]
        public class EntityBehaviorTemporalStabilityAffected_OnGameTick_Patch
        {
            private static readonly System.Collections.Generic.Dictionary<long, long> LastCheckByEntityId = new();
            private const int CheckIntervalMs = 250; // Check only 4 times per second instead of every tick
            
            public static void Postfix(EntityBehaviorTemporalStabilityAffected __instance)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_EntityBehaviorTemporalStabilityAffected_OnGameTick)) return;
                if (__instance?.entity is not EntityPlayer player) return;
                
                // Throttle checks to reduce CPU usage
                long entityId = player.EntityId;
                long nowMs = player.World?.ElapsedMilliseconds ?? 0;
                if (LastCheckByEntityId.TryGetValue(entityId, out long lastCheck) && (nowMs - lastCheck) < CheckIntervalMs)
                {
                    return;
                }
                LastCheckByEntityId[entityId] = nowMs;
                
                // Cleanup occasionally
                if (LastCheckByEntityId.Count > 512)
                {
                    LastCheckByEntityId.Clear();
                }

                if (!EnableTemporalStabilityWearablePatch) return;

                var stats = WearableStatsCache.GetCachedStats(player);
                // Fast exit: check cached value first before stability read
                if (stats == null || Math.Abs(stats.TemporalDrainReduction) <= 0.0001f) return;
                
                double currentStability = __instance.OwnStability;
                double prevStability = player.WatchedAttributes.GetDouble("vsquest:temporal:prev", currentStability);
                
                // Store current for next check
                player.WatchedAttributes.SetDouble("vsquest:temporal:prev", currentStability);
                
                // Only adjust when stability decreases (drain)
                double delta = currentStability - prevStability;
                if (delta >= 0) return; // Not draining or gained stability
                
                // Calculate actual drain without our modification
                // The vanilla drain happened between prev and current
                float mult = GameMath.Clamp(1f - stats.TemporalDrainReduction, 0f, 3f);
                double adjusted = prevStability + delta * mult;
                __instance.OwnStability = GameMath.Clamp(adjusted, 0.0, 1.0);
            }
        }

        [HarmonyPatch(typeof(EntityBehaviorBodyTemperature), "updateWearableConditions")]
        public class EntityBehaviorBodyTemperature_updateWearableConditions_Warmth_Patch
        {
            public static void Postfix(EntityBehaviorBodyTemperature __instance)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_EntityBehaviorBodyTemperature_WarmthBonus)) return;
                
                var eagent = __instance.entity as EntityAgent;
                if (eagent == null) return;

                // Get inventory via reflection to avoid VSEssentials dependency
                var bh = eagent.GetBehavior("EntityBehaviorPlayerInventory");
                if (bh == null) return;

                var invField = bh.GetType().GetProperty("Inventory");
                if (invField == null) return;

                var inventory = invField.GetValue(bh) as IInventory;
                if (inventory == null) return;

                float bonusWarmth = 0f;
                foreach (var slot in inventory)
                {
                    if (slot.Empty) continue;
                    
                    bonusWarmth += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrWarmth);
                }

                if (bonusWarmth > 0f)
                {
                    // Add bonus to clothingBonus via reflection
                    var field = typeof(EntityBehaviorBodyTemperature).GetField("clothingBonus", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        float currentBonus = (float)field.GetValue(__instance);
                        field.SetValue(__instance, currentBonus + bonusWarmth);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CollectibleObject), "GetMiningSpeed")]
        public class CollectibleObject_GetMiningSpeed_MiningSpeedMult_Patch
        {
            public static void Postfix(CollectibleObject __instance, IItemStack itemstack, BlockSelection blockSel, Block block, IPlayer forPlayer, ref float __result)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_CollectibleObject_GetMiningSpeed_MiningSpeedMult)) return;
                if (__result <= 0f) return;
                if (forPlayer?.Entity is not EntityPlayer player) return;

                var stats = WearableStatsCache.GetCachedStats(player);
                if (stats == null || Math.Abs(stats.MiningSpeedMult) < 0.0001f) return;

                float mult = GameMath.Clamp(1f + stats.MiningSpeedMult, 0f, 10f);
                __result *= mult;
            }
        }

        [HarmonyPatch(typeof(ModSystemWearableStats), "handleDamaged")]
        public class ModSystemWearableStats_handleDamaged_Patch
        {
            // Damage types that protection applies to
            private static bool IsPhysicalDamage(DamageSource dmgSource)
            {
                if (dmgSource == null) return false;
                var type = dmgSource.Type;
                return type == EnumDamageType.BluntAttack
                    || type == EnumDamageType.SlashingAttack
                    || type == EnumDamageType.PiercingAttack
                    || type == EnumDamageType.Crushing
                    || type == EnumDamageType.Injury;
            }

            public static void Postfix(ModSystemWearableStats __instance, IPlayer player, float damage, DamageSource dmgSource, ref float __result)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_ModSystemWearableStats_handleDamaged)) return;
                if (__result <= 0f) return;
                if (player?.Entity is not EntityPlayer entity) return;

                // Only apply protection to physical damage types
                if (!IsPhysicalDamage(dmgSource)) return;

                var stats = WearableStatsCache.GetCachedStats(entity);
                if (stats == null) return;

                float newDamage = __result;
                newDamage = System.Math.Max(0f, newDamage - stats.FlatProtection);

                __result = newDamage;
            }
        }

        [HarmonyPatch(typeof(ModSystemWearableStats), "updateWearableStats")]
        public class ModSystemWearableStats_updateWearableStats_Patch
        {
            // Throttling for walkSpeed updates to prevent jitter when updateWearableStats is called frequently (e.g. on damage)
            private static readonly Dictionary<long, long> LastWalkSpeedUpdateByEntityId = new Dictionary<long, long>();
            private const int WalkSpeedUpdateThrottleMs = 500;

            private const string LastAppliedPrefix = "vsquest:wearablestats:last:";
            private const float CompareEpsilon = 0.0001f;

            private static int _cleanupCounter = 0;
            
            // Inventory change tracker to avoid recalculating on every damage tick
            private static InventoryChangeTracker _inventoryTracker;
            
            private static InventoryChangeTracker GetTracker(ICoreAPI api)
            {
                if (_inventoryTracker == null && api != null)
                {
                    _inventoryTracker = new InventoryChangeTracker(api);
                }
                return _inventoryTracker;
            }

            private static bool ApproximatelyEqual(float a, float b)
            {
                if (float.IsNaN(a) && float.IsNaN(b)) return true;
                if (float.IsNaN(a) || float.IsNaN(b)) return false;
                return Math.Abs(a - b) <= CompareEpsilon;
            }

            public static void Postfix(ModSystemWearableStats __instance, IInventory inv, IServerPlayer player)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_ModSystemWearableStats_updateWearableStats)) return;
                if (player?.Entity?.Stats == null) return;

                // Skip recalculation if inventory hasn't changed (avoids recalc on damage)
                if (player.Entity is EntityPlayer entityPlayer)
                {
                    var tracker = GetTracker(player.Entity.Api);
                    if (tracker != null && !tracker.ShouldRecalculate(entityPlayer))
                    {
                        return; // Equipment unchanged, use cached values
                    }
                }

                // Local variables replacing StatModifiers struct (type removed in newer VS versions)
                float walkSpeed = 0f;
                float hungerrate = 0f;
                float healingeffectivness = 0f;
                float rangedWeaponsAcc = 0f;
                float rangedWeaponsSpeed = 0f;
                float miningSpeedMult = 0f;
                float jumpHeightMult = 0f;
                float maxHealthFlat = 0f;
                float maxOxygenBonus = 0f;

                foreach (ItemSlot slot in inv)
                {
                    if (!slot.Empty && slot.Itemstack?.Collectible.GetCollectibleInterface<Vintagestory.API.Common.IWearable>() != null)
                    {
                        var stack = slot.Itemstack;
                        walkSpeed += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrWalkSpeed);
                        hungerrate += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrHungerRate);
                        healingeffectivness += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrHealingEffectiveness);
                        rangedWeaponsAcc += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrRangedAccuracy);
                        rangedWeaponsSpeed += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrRangedSpeed);
                        miningSpeedMult += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrMiningSpeedMult);
                        jumpHeightMult += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrJumpHeightMul);
                        maxHealthFlat += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrMaxHealthFlat);
                        maxOxygenBonus += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrMaxOxygen);
                    }
                }

                // Batch read last applied values from WatchedAttributes using TreeAttribute
                var wa = player.Entity.WatchedAttributes;
                var lastTree = wa.GetTreeAttribute(LastAppliedPrefix.TrimEnd(':'));
                
                bool changed = true;
                if (lastTree != null)
                {
                    changed =
                        !ApproximatelyEqual(lastTree.GetFloat("walkspeed", float.NaN), walkSpeed) ||
                        !ApproximatelyEqual(lastTree.GetFloat("healing", float.NaN), healingeffectivness) ||
                        !ApproximatelyEqual(lastTree.GetFloat("hunger", float.NaN), hungerrate) ||
                        !ApproximatelyEqual(lastTree.GetFloat("acc", float.NaN), rangedWeaponsAcc) ||
                        !ApproximatelyEqual(lastTree.GetFloat("rangeSpeed", float.NaN), rangedWeaponsSpeed) ||
                        !ApproximatelyEqual(lastTree.GetFloat("mining", float.NaN), miningSpeedMult) ||
                        !ApproximatelyEqual(lastTree.GetFloat("jump", float.NaN), jumpHeightMult) ||
                        !ApproximatelyEqual(lastTree.GetFloat("health", float.NaN), maxHealthFlat) ||
                        !ApproximatelyEqual(lastTree.GetFloat("oxygen", float.NaN), maxOxygenBonus);
                }

                if (!changed) return;

                // Use local dictionary to avoid lock contention with multiple players
                var statsDict = new Dictionary<string, float>(7);
                if (player.Entity is EntityPlayer ep && ep.Api is ICoreServerAPI sapi)
                {
                    statsDict["walkspeed"] = walkSpeed;
                    statsDict["healingeffectivness"] = healingeffectivness;
                    statsDict["hungerrate"] = hungerrate;
                    statsDict["rangedWeaponsAcc"] = rangedWeaponsAcc;
                    statsDict["rangedWeaponsSpeed"] = rangedWeaponsSpeed;
                    statsDict["miningSpeedMul"] = miningSpeedMult;
                    statsDict["jumpHeightMul"] = jumpHeightMult;

                    StatCoalescingEngine.QueueStatUpdates(sapi, ep, statsDict);
                }
                else
                {
                    // Fallback to direct updates if coalescing not available
                    player.Entity.Stats.Set("walkspeed", "vsquestmod", walkSpeed, true);
                    player.Entity.Stats.Set("healingeffectivness", "vsquestmod", healingeffectivness, true);
                    player.Entity.Stats.Set("hungerrate", "vsquestmod", hungerrate, true);
                    player.Entity.Stats.Set("rangedWeaponsAcc", "vsquestmod", rangedWeaponsAcc, true);
                    player.Entity.Stats.Set("rangedWeaponsSpeed", "vsquestmod", rangedWeaponsSpeed, true);
                    player.Entity.Stats.Set("miningSpeedMul", "vsquestmod", miningSpeedMult, true);
                    player.Entity.Stats.Set("jumpHeightMul", "vsquestmod", jumpHeightMult, true);
                }

                var healthBehavior = player.Entity.GetBehavior<EntityBehaviorHealth>();
                if (healthBehavior != null)
                {
                    healthBehavior.SetMaxHealthModifiers("vsquestmod:attr:maxhealth", maxHealthFlat);
                }

                var oxygenBehavior = player.Entity.GetBehavior<EntityBehaviorBreathe>();
                if (oxygenBehavior != null)
                {
                    const string AppliedBonusKey = "alegacyvsquest:attr:maxoxygenbonusapplied";

                    float lastAppliedBonus = player.Entity.WatchedAttributes.GetFloat(AppliedBonusKey, 0f);

                    // Calculate a stable base that doesn't stack when updateWearableStats runs multiple times.
                    // We treat the current MaxOxygen as (base + lastAppliedBonus).
                    float baseOxygen = oxygenBehavior.MaxOxygen - lastAppliedBonus;
                    if (baseOxygen < 1f) baseOxygen = 1f;

                    float newMaxOxygen = Math.Max(1f, baseOxygen + maxOxygenBonus);
                    oxygenBehavior.MaxOxygen = newMaxOxygen;

                    // If max oxygen decreased (e.g. accessory removed), ensure current oxygen cannot exceed the new max.
                    float currentOxygen = oxygenBehavior.Oxygen;
                    if (currentOxygen > newMaxOxygen)
                    {
                        oxygenBehavior.Oxygen = newMaxOxygen;
                    }
                    player.Entity.WatchedAttributes.SetFloat(AppliedBonusKey, maxOxygenBonus);
                }

                // Throttle walkSpeed updates - cleanup only every 1000 calls
                long nowMs = player.Entity.World?.ElapsedMilliseconds ?? 0;
                long entityId = player.Entity.EntityId;
                
                if (!LastWalkSpeedUpdateByEntityId.TryGetValue(entityId, out long lastUpdate) || 
                    (nowMs - lastUpdate) >= WalkSpeedUpdateThrottleMs)
                {
                    player.Entity.UpdatePlayerWalkSpeed();
                    LastWalkSpeedUpdateByEntityId[entityId] = nowMs;

                    // Cleanup only occasionally to prevent memory bloat
                    if (++_cleanupCounter > 1000 && LastWalkSpeedUpdateByEntityId.Count > 512)
                    {
                        LastWalkSpeedUpdateByEntityId.Clear();
                        _cleanupCounter = 0;
                    }
                }
                
                // Invalidate cache when wearable stats are updated
                if (player.Entity is EntityPlayer entityPlayer2)
                {
                    WearableStatsCache.InvalidateCache(entityPlayer2);
                }

                // Batch write last applied values to WatchedAttributes using TreeAttribute
                var newTree = new Vintagestory.API.Datastructures.TreeAttribute();
                newTree.SetFloat("walkspeed", walkSpeed);
                newTree.SetFloat("healing", healingeffectivness);
                newTree.SetFloat("hunger", hungerrate);
                newTree.SetFloat("acc", rangedWeaponsAcc);
                newTree.SetFloat("rangeSpeed", rangedWeaponsSpeed);
                newTree.SetFloat("mining", miningSpeedMult);
                newTree.SetFloat("jump", jumpHeightMult);
                newTree.SetFloat("health", maxHealthFlat);
                newTree.SetFloat("oxygen", maxOxygenBonus);
                wa.SetAttribute(LastAppliedPrefix.TrimEnd(':'), newTree);
            }
        }
    }
}
