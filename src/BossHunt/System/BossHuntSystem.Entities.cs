using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public partial class BossHuntSystem
    {
        private void TryDespawnBossEntity(BossHuntConfig cfg)
        {
            if (sapi == null || cfg == null) return;

            var bossEntity = entityTracker?.GetTrackedEntityAny(cfg.bossKey) ?? FindBossEntityImmediateAny(cfg.bossKey);
            if (bossEntity == null) return;

            TryDespawnBossEntity(bossEntity);
        }

        private void TryDespawnBossEntity(Entity bossEntity)
        {
            if (sapi == null || bossEntity == null) return;

            try
            {
                sapi.World.DespawnEntity(bossEntity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[BossHuntSystem.Entities] Failed to despawn boss '{0}': {1}", bossEntity.Code, ex.Message);
            }
        }

        private Entity FindBossEntityImmediateAny(string bossTargetId)
        {
            if (sapi == null) return null;
            if (string.IsNullOrWhiteSpace(bossTargetId)) return null;

            var loaded = sapi.World?.LoadedEntities;
            if (loaded == null) return null;

            try
            {
                foreach (var e in loaded.Values)
                {
                    if (e == null) continue;
                    var qt = e.GetBehavior<EntityBehaviorQuestTarget>();
                    if (qt == null) continue;

                    if (string.Equals(qt.TargetId, bossTargetId, StringComparison.OrdinalIgnoreCase))
                    {
                        return e;
                    }
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[BossHuntSystem.Entities] Failed to find boss entity: {0}", ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Safety: if multiple live entities exist for the same bossKey, keep the one
        /// already tracked (or the first found) and despawn all duplicates.
        /// </summary>
        private void EnforceSingleLiveBoss(string bossKey)
        {
            if (sapi == null || string.IsNullOrWhiteSpace(bossKey)) return;

            var loaded = sapi.World?.LoadedEntities;
            if (loaded == null) return;

            Entity keeper = entityTracker?.GetTrackedEntity(bossKey);
            if (keeper == null || !keeper.Alive)
            {
                keeper = null;
            }

            try
            {
                foreach (var e in loaded.Values)
                {
                    if (e == null) continue;
                    // Skip old-phase entities during rebirth transition
                    if (e.WatchedAttributes?.GetBool(EntityBehaviorBossRebirth2.RebirthOldPhaseKey, false) == true) continue;
                    var qt = e.GetBehavior<EntityBehaviorQuestTarget>();
                    if (qt == null) continue;
                    if (!string.Equals(qt.TargetId, bossKey, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!e.Alive) continue;

                    if (keeper == null)
                    {
                        keeper = e;
                        continue;
                    }

                    if (e == keeper) continue;

                    try
                    {
                        sapi.World.DespawnEntity(e, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Warning("[BossHuntSystem.Entities] Failed to despawn duplicate boss '{0}': {1}", bossKey, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[BossHuntSystem.Entities] Failed to enforce single live boss '{0}': {1}", bossKey, ex.Message);
            }
        }


        private void TrySpawnBoss(BossHuntConfig cfg, Vec3d point, int dim, BossHuntAnchorPoint anchorPoint)
        {
            if (cfg == null) return;
            if (point == null) return;

            // Safety: do not spawn a new boss if a living one with the same targetId already exists.
            var existing = entityTracker?.GetTrackedEntity(cfg.bossKey);
            if (existing != null && existing.Alive)
            {
                return;
            }

            double nowHours = sapi.World?.Calendar?.TotalHours ?? 0;
            var stateMachine = GetOrCreateStateMachine(cfg.bossKey);

            // Do not spawn if a rebirth transition is currently in progress
            if (stateMachine != null && stateMachine.CurrentState == BossCombatState.Rebirthing)
            {
                DebugLog($"Spawn blocked: bossKey={cfg.bossKey} is currently rebirthing.");
                return;
            }

            stateMachine?.OnSpawn(nowHours);

            try
            {
                var type = sapi.World.GetEntityType(new AssetLocation(cfg.GetBossEntityCode()));
                if (type == null)
                {
                    DebugLog($"Spawn failed: entity type not found for code '{cfg.GetBossEntityCode()}'.", force: true);
                    return;
                }

                Entity entity = sapi.World.ClassRegistry.CreateEntity(type);
                if (entity == null)
                {
                    DebugLog($"Spawn failed: entity create returned null for code '{cfg.GetBossEntityCode()}'.", force: true);
                    return;
                }

                if (entity.WatchedAttributes != null)
                {
                    entity.WatchedAttributes.SetString("alegacyvsquest:killaction:targetid", cfg.bossKey);
                    entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:killaction:targetid");
                }

                EntityBehaviorQuestTarget.SetSpawnerAnchor(entity, new BlockPos((int)point.X, (int)point.Y, (int)point.Z, dim));
                if (anchorPoint != null && entity.WatchedAttributes != null)
                {
                    if (anchorPoint.leashRange > 0f)
                    {
                        entity.WatchedAttributes.SetFloat(EntityBehaviorQuestTarget.LeashRangeKey, anchorPoint.leashRange);
                        entity.WatchedAttributes.MarkPathDirty(EntityBehaviorQuestTarget.LeashRangeKey);
                    }

                    entity.WatchedAttributes.SetFloat(EntityBehaviorQuestTarget.OutOfCombatLeashRangeKey, anchorPoint.outOfCombatLeashRange);
                    entity.WatchedAttributes.MarkPathDirty(EntityBehaviorQuestTarget.OutOfCombatLeashRangeKey);
                }

                entity.Pos.SetPosWithDimension(new Vec3d(point.X, point.Y + dim * 32768.0, point.Z));
                entity.Pos.SetFrom(entity.Pos);

                sapi.World.SpawnEntity(entity);

                // Force entity tracker to scan immediately for the new entity
                entityTracker?.ForceScan();

                // Schedule next relocate and soft reset for the newly spawned boss
                double spawnNowHours = sapi.World?.Calendar?.TotalHours ?? 0;
                var st = GetOrCreateState(cfg.bossKey);
                EnsureRelocateTimerInitialized(st, cfg, spawnNowHours);
                ScheduleRelocate(cfg.bossKey, st.nextRelocateAtTotalHours);
                ScheduleSoftReset(cfg.bossKey, spawnNowHours + softResetIdleHours);
                CancelScheduledDeadCooldown(cfg.bossKey);
            }
            catch (Exception ex)
            {
                sapi?.Logger.Warning("[BossHuntSystem.Entities] Failed to spawn boss '{0}': {1}", cfg.bossKey, ex.Message);
            }
        }

        private void DebugLog(string message, bool force = false)
        {
            if (!debugBossHunt || sapi == null || string.IsNullOrWhiteSpace(message)) return;

            double nowHours = sapi.World?.Calendar?.TotalHours ?? 0;
            if (!force && nowHours < nextDebugLogTotalHours) return;

            double throttle = debugLogThrottleHours;
            if (throttle <= 0) throttle = 0.02;
            nextDebugLogTotalHours = nowHours + throttle;
            sapi.Logger.Notification($"[BossHunt] {message}");
        }

        private int PickAnotherIndex(int current, int count)
        {
            if (count <= 1) return 0;

            int next = current;
            try
            {
                var rand = sapi?.World?.Rand;
                if (rand != null)
                {
                    for (int i = 0; i < 5 && next == current; i++)
                    {
                        next = rand.Next(0, count);
                    }
                }
            }
            catch
            {
                // fallback below
            }

            if (next == current)
            {
                next = (current + 1) % count;
            }

            return next;
        }

        private bool AnyPlayerNear(Vec3d point, int dim, float range)
        {
            if (point == null) return false;

            return AnyPlayerNear(point.X, point.Y, point.Z, dim, range);
        }

        private bool AnyPlayerNear(double x, double y, double z, int dim, float range)
        {
            if (range <= 0) range = 160f;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return false;

            double rangeSq = range * (double)range;

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe?.Pos == null) continue;
                if (pe.Pos.Dimension != dim) continue;

                double dx = pe.Pos.X - x;
                double dy = pe.Pos.Y - y;
                double dz = pe.Pos.Z - z;

                if (dx * dx + dy * dy + dz * dz <= rangeSq) return true;
            }

            return false;
        }

        private bool IsSafeToRelocate(BossHuntConfig cfg, Entity bossEntity, double nowHours)
        {
            if (bossEntity == null) return true;

            // Do not relocate while summon ritual is active
            if (bossEntity.GetBehavior<EntityBehaviorBossSummonRitual>()?.IsRitualActive == true) return false;

            // Do not relocate while intermission is active (adds may still be alive)
            if (bossEntity.GetBehavior<EntityBehaviorBossIntermissionDispel>()?.IsInIntermission == true) return false;

            var stateMachine = GetOrCreateStateMachine(cfg.bossKey);
            if (stateMachine == null) return true;

            // Don't relocate if boss is currently in combat
            if (stateMachine.CurrentState == BossCombatState.InCombat)
            {
                return false;
            }

            // Check if damage occurred recently (legacy logic for no relocate after damage)
            double lastDamage = bossEntity.WatchedAttributes.GetDouble(LastBossDamageTotalHoursKey, double.NaN);
            if (!double.IsNaN(lastDamage))
            {
                double lockHours = cfg.GetNoRelocateAfterDamageHours();
                if (lockHours > 0 && nowHours - lastDamage < lockHours)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (sapi == null || entity == null) return;
            if (sapi.World == null || sapi.World.Calendar == null) return;

            if (configs == null || configs.Count == 0) return;

            var qt = entity.GetBehavior<EntityBehaviorQuestTarget>();
            if (qt == null) return;

            for (int i = 0; i < configs.Count; i++)
            {
                var cfg = configs[i];
                if (cfg == null) continue;

                var bossTargetId = cfg.bossKey;

                if (!string.Equals(qt.TargetId, bossTargetId, StringComparison.OrdinalIgnoreCase)) continue;

                var st = GetOrCreateState(cfg.bossKey);
                var stateMachine = GetOrCreateStateMachine(cfg.bossKey);

                stateMachine?.OnDeath();

                double nowHours = sapi.World.Calendar.TotalHours;
                st.deadUntilTotalHours = nowHours + cfg.GetRespawnHours();

                // Rotate location on death to prevent camping.
                st.currentPointIndex = PickAnotherIndex(st.currentPointIndex, GetPointCount(cfg, st));
                st.nextRelocateAtTotalHours = nowHours + cfg.GetRelocateIntervalHours();

                stateDirty = true;

                ScheduleDeadCooldown(cfg.bossKey, st.deadUntilTotalHours);
                ScheduleRelocate(cfg.bossKey, st.nextRelocateAtTotalHours);
                CancelScheduledSoftReset(cfg.bossKey);
                return;
            }
        }
    }
}
