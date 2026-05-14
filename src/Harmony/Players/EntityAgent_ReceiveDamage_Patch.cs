using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using VsQuest;
using VsQuest.Harmony.Items;

namespace VsQuest.Harmony.Players
{
    [HarmonyPatch(typeof(EntityAgent), "ReceiveDamage")]
    public class EntityAgent_ReceiveDamage_Patch
    {
        // Cached keys to avoid string allocations
        private static class AttrKeys
        {
            public const string BossCloneInvulnerable = "alegacyvsquest:bossclone:invulnerable";
            public const string BossClone = "alegacyvsquest:bossclone";
            public const string BossCloneOwnerId = "alegacyvsquest:bossclone:ownerid";
            public const string BossCloneDamageMult = "alegacyvsquest:bossclone:damagemult";
            public const string BossGrowthRitualDamageMult = "alegacyvsquest:bossgrowthritual:damagemult";
            public const string AdminAttackPower = "vsquestadmin:attr:attackpower";
            public const string FiredBy = "firedBy";
            public const string HasInvulnBehavior = "vsquest:hasinvuln";
            public const string InvulnCheckTime = "vsquest:invulncheck";
        }

        private const int InvulnCheckIntervalMs = 5000; // Only check HasBehavior every 5 seconds

        public static bool Prefix(EntityAgent __instance, DamageSource damageSource, ref float damage, ref bool __result)
        {
            if (!HarmonyPatchSwitches.PlayerEnabled(HarmonyPatchSwitches.Player_EntityAgent_ReceiveDamage_PlayerAttackPower)) return true;

            // Ultra-fast rejects first (3 simple comparisons)
            if (damage <= 0f) return true; // Run original
            if (damageSource == null) return true; // Run original
            if (__instance?.Api?.Side != EnumAppSide.Server) return true; // Run original

            // 1. Single attribute fetch to minimize tree traversals
            var watchedAttrs = __instance.WatchedAttributes;
            if (watchedAttrs == null) return true;

            // Fast boss check - uses cached result
            bool isBoss = BossDetection.IsBossTargetFast(__instance);

            // Boss clone invulnerability check
            if (watchedAttrs.GetBool(AttrKeys.BossCloneInvulnerable, false))
            {
                damage = 0f;
                if (damageSource != null) damageSource.KnockbackStrength = 0f;
                __result = false;
                return false;
            }

            // Single cached computation of source/cause entities
            var sourceEntity = damageSource?.SourceEntity;
            var causeEntity = damageSource?.GetCauseEntity() ?? sourceEntity;
            var sourceAttrs = sourceEntity?.WatchedAttributes ?? causeEntity?.WatchedAttributes;

            // Boss knockback immunity (zero knockback for bosses)
            if (isBoss && damageSource != null && damageSource.KnockbackStrength > 0f)
            {
                damageSource.KnockbackStrength = 0f;
            }

            // Knockback bonus from attacker gear - only if knockback still present and attacker is player
            if (damageSource != null && damageSource.KnockbackStrength > 0f && causeEntity is EntityPlayer attacker && attacker.Player?.InventoryManager != null)
            {
                var cached = WearableStatsCache.GetCachedStats(attacker);
                if (cached != null && cached.KnockbackMult != 0f)
                {
                    damageSource.KnockbackStrength *= GameMath.Clamp(1f + cached.KnockbackMult, 0f, 5f);
                }
            }

            // Attack power bonus from attacker's equipped wearables
            if (causeEntity is EntityPlayer attackPlayer && attackPlayer.Player?.InventoryManager != null)
            {
                var cached = WearableStatsCache.GetCachedStats(attackPlayer);
                if (cached != null)
                {
                    // Flat attack power bonus - applies to all damage types
                    if (Math.Abs(cached.AttackPower) > 0.0001f)
                    {
                        damage += cached.AttackPower;
                        if (damage < 0.1f) damage = 0.1f; // Minimum damage
                    }
                    
                    // Melee-only attack power bonus
                    if (Math.Abs(cached.MeleeAttackPower) > 0.0001f)
                    {
                        bool isMelee = damageSource?.SourceEntity == null || damageSource.SourceEntity == causeEntity;
                        if (isMelee)
                        {
                            damage += cached.MeleeAttackPower;
                            if (damage < 0.1f) damage = 0.1f;
                        }
                    }
                    
                    // Ranged damage multiplier for projectiles (arrows, spears, stones, etc.)
                    if (cached.RangedDamageMult != 0f && damageSource?.SourceEntity != null)
                    {
                        // Check if it's a projectile fired by player (source entity is different from cause entity)
                        if (damageSource.SourceEntity != causeEntity)
                        {
                            damage *= GameMath.Clamp(1f + cached.RangedDamageMult, 0.1f, 5f);
                        }
                    }
                }
            }

            // Boss clone damage handling
            if (sourceAttrs != null && sourceAttrs.GetBool(AttrKeys.BossClone, false))
            {
                long ownerId = sourceAttrs.GetLong(AttrKeys.BossCloneOwnerId, 0);
                if (ownerId > 0 && __instance.EntityId == ownerId)
                {
                    damage = 0f;
                    if (damageSource != null) damageSource.KnockbackStrength = 0f;
                    __result = false;
                    return false;
                }

                // Damage multiplier from clone
                float mult = sourceAttrs.GetFloat(AttrKeys.BossCloneDamageMult, 1f);
                if (mult >= 0f && mult < 0.999f)
                {
                    damage *= mult;
                }
            }

            // Boss growth ritual damage multiplier (when boss is the attacker)
            if (sourceEntity != null)
            {
                var sourceWatchedAttrs = sourceEntity.WatchedAttributes;
                if (sourceWatchedAttrs != null)
                {
                    float growthMult = sourceWatchedAttrs.GetFloat(AttrKeys.BossGrowthRitualDamageMult, 0f);
                    if (growthMult > 1.01f)
                    {
                        damage *= growthMult;
                    }
                }
            }

            // Fired by boss clone check (rare)
            if (sourceEntity != null && sourceAttrs?.HasAttribute(AttrKeys.FiredBy) == true)
            {
                long firedById = sourceAttrs.GetLong(AttrKeys.FiredBy, 0);
                if (firedById > 0 && __instance.World?.GetEntityById(firedById) is Entity firedBy)
                {
                    var fbAttrs = firedBy.WatchedAttributes;
                    if (fbAttrs?.GetBool(AttrKeys.BossClone, false) == true)
                    {
                        long ownerId = fbAttrs.GetLong(AttrKeys.BossCloneOwnerId, 0);
                        if (ownerId > 0 && __instance.EntityId == ownerId)
                        {
                            damage = 0f;
                            if (damageSource != null) damageSource.KnockbackStrength = 0f;
                            __result = false;
                            return false;
                        }
                    }
                }
            }

            // Boss damage invulnerability check
            if (HasInvulnerabilityBehaviorCached(__instance, out bool hasInvuln) && hasInvuln)
            {
                long now = __instance.Api.World.ElapsedMilliseconds;
                
                if (EntityBehaviorBossDamageInvulnerability.ShouldBlockDamage(__instance, now))
                {
                    damage = 0f;
                    if (damageSource != null) damageSource.KnockbackStrength = 0f;
                    __result = false;
                    return false;
                }
                
                if (damage > 0)
                {
                    var behavior = __instance.GetBehavior<EntityBehaviorBossDamageInvulnerability>();
                    int durationMs = behavior?.DurationMs ?? 1000;
                    EntityBehaviorBossDamageInvulnerability.OnDamageReceived(__instance, durationMs);
                }
            }

            // Continue to original method
            return true;
        }

        /// <summary>
        /// Cached check for invulnerability behavior - avoids expensive HasBehavior calls every tick.
        /// Returns true if cache valid, outputs result in hasInvuln.
        /// </summary>
        private static bool HasInvulnerabilityBehaviorCached(EntityAgent entity, out bool hasInvuln)
        {
            hasInvuln = false;
            if (entity?.WatchedAttributes == null) return false;

            long now = entity.World?.ElapsedMilliseconds ?? 0;
            var tree = entity.WatchedAttributes.GetTreeAttribute(AttrKeys.HasInvulnBehavior);
            
            if (tree != null)
            {
                long checkTime = tree.GetLong(AttrKeys.InvulnCheckTime);
                if (now - checkTime < InvulnCheckIntervalMs)
                {
                    hasInvuln = tree.GetBool("has");
                    return true;
                }
            }

            // Cache expired or missing - do expensive check and cache result
            hasInvuln = entity.HasBehavior<EntityBehaviorBossDamageInvulnerability>();
            
            var newTree = new TreeAttribute();
            newTree.SetBool("has", hasInvuln);
            newTree.SetLong(AttrKeys.InvulnCheckTime, now);
            entity.WatchedAttributes.SetAttribute(AttrKeys.HasInvulnBehavior, newTree);
            
            return true;
        }
    }

    public static class BossDetection
    {
        private const string BossTargetKey = "vsquest:isBossTarget";
        private const string BossTag = "albase-boss";
        private const string BossCheckTimeKey = "vsquest:bosschecktime";
        private const int BossCheckIntervalMs = 10000; // 10 seconds

        /// <summary>
        /// Fast boss check - uses cached result if available, avoids expensive HasBehavior calls.
        /// </summary>
        public static bool IsBossTargetFast(EntityAgent target)
        {
            if (target == null) return false;

            // Fast path 1: cached attribute
            var watched = target.WatchedAttributes;
            if (watched == null) return false;
            
            if (watched.GetBool(BossTargetKey, false))
                return true;

            // Fast path 2: Check if we've recently verified this is NOT a boss
            long now = target.World?.ElapsedMilliseconds ?? 0;
            var tree = watched.GetTreeAttribute(BossCheckTimeKey);
            if (tree != null)
            {
                long lastCheck = tree.GetLong("time");
                if (now - lastCheck < BossCheckIntervalMs)
                {
                    return tree.GetBool("isboss", false);
                }
            }

            // Full check needed
            bool isBoss = IsBossTargetInternal(target);
            
            // Cache result
            var newTree = new TreeAttribute();
            newTree.SetLong("time", now);
            newTree.SetBool("isboss", isBoss);
            watched.SetAttribute(BossCheckTimeKey, newTree);
            
            return isBoss;
        }

        /// <summary>
        /// Standard boss check - slower but always accurate.
        /// </summary>
        public static bool IsBossTarget(EntityAgent target)
        {
            if (target == null) return false;

            // Fast path: cached attribute
            if (target.WatchedAttributes?.GetBool(BossTargetKey, false) == true)
                return true;

            return IsBossTargetInternal(target);
        }

        private static bool IsBossTargetInternal(EntityAgent target)
        {
            // Check for boss tag
            if (target.Properties?.Attributes?["alegacy-boss"].AsBool(false) == true)
                return true;
            if (target.Properties?.Attributes?["boss"].AsBool(false) == true)
                return true;

            // Fallback: entity code patterns
            string codePath = target.Code?.Path;
            if (codePath != null)
            {
                if (codePath.Contains("boss") ||
                    codePath.Contains("bosshunt") ||
                    codePath.Contains("miniboss") ||
                    codePath.Contains("raidboss"))
                    return true;
            }

            // Last resort: Check behaviors - combined into single iteration
            var behaviors = target.SidedProperties?.Behaviors;
            if (behaviors != null)
            {
                foreach (var behavior in behaviors)
                {
                    if (behavior is EntityBehaviorBossCombatMarker ||
                        behavior is EntityBehaviorBossHuntCombatMarker ||
                        behavior is EntityBehaviorQuestBoss)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
    }
}
