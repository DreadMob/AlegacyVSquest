using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public partial class HollowTrialSystem
    {
        private void TrySpawnIfPlayerNearby(HollowTrialConfig cfg, HollowTrialStateEntry entry, double nowHours)
        {
            if (cfg == null || entry == null) return;
            if (entry.anchorPoints == null || entry.anchorPoints.Count == 0) return;

            // Use first anchor point (trials don't relocate)
            var anchor = entry.anchorPoints[0];
            var point = new Vec3d(anchor.x, anchor.y + anchor.yOffset, anchor.z);
            int dim = anchor.dim;

            float range = cfg.GetActivationRange(coreConfig);
            if (!AnyPlayerNear(point.X, point.Y, point.Z, dim, range)) return;

            TrySpawnBoss(cfg, point, dim, anchor);
        }

        private void TrySpawnBoss(HollowTrialConfig cfg, Vec3d point, int dim, HollowTrialAnchorPoint anchor)
        {
            if (cfg == null || point == null) return;

            // Don't spawn if already alive
            var existing = entityTracker?.GetTrackedEntity(cfg.trialKey);
            if (existing != null && existing.Alive) return;

            try
            {
                var entityCode = cfg.GetEntityCode();
                var type = sapi.World.GetEntityType(new AssetLocation(entityCode));
                if (type == null)
                {
                    DebugLog($"Spawn failed: entity type not found for '{entityCode}'");
                    return;
                }

                Entity entity = sapi.World.ClassRegistry.CreateEntity(type);
                if (entity == null)
                {
                    DebugLog($"Spawn failed: entity create returned null for '{entityCode}'");
                    return;
                }

                // Set quest target ID so killactiontarget objective works
                if (entity.WatchedAttributes != null)
                {
                    entity.WatchedAttributes.SetString("alegacyvsquest:killaction:targetid", cfg.trialKey);
                    entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:killaction:targetid");
                }

                // Set spawner anchor for leash behavior
                EntityBehaviorQuestTarget.SetSpawnerAnchor(entity, new BlockPos((int)point.X, (int)point.Y, (int)point.Z, dim));

                entity.Pos.SetPosWithDimension(new Vec3d(point.X, point.Y + dim * 32768.0, point.Z));
                entity.Pos.SetFrom(entity.Pos);

                sapi.World.SpawnEntity(entity);
                entityTracker?.ForceScan();

                DebugLog($"Spawned trial boss '{cfg.trialKey}' at {point.X:0},{point.Y:0},{point.Z:0} dim={dim}");
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[HollowTrialSystem] Failed to spawn trial boss '{0}': {1}", cfg.trialKey, ex.Message);
            }
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (sapi == null || entity == null) return;

            var qt = entity.GetBehavior<EntityBehaviorQuestTarget>();
            if (qt == null || string.IsNullOrWhiteSpace(qt.TargetId)) return;

            var cfg = FindConfig(qt.TargetId);
            if (cfg == null) return;

            // This is a trial boss death
            var entry = GetOrCreateEntry(cfg.trialKey);
            double nowHours = sapi.World.Calendar.TotalHours;

            entry.deadUntilTotalHours = nowHours + cfg.GetRespawnHours(coreConfig);
            stateDirty = true;

            DebugLog($"Trial boss '{cfg.trialKey}' died. Respawn at {entry.deadUntilTotalHours:0.0}h (in {cfg.GetRespawnHours(coreConfig):0.0}h)");
        }

        /// <summary>
        /// Force respawn a specific trial boss. Used by admin command.
        /// </summary>
        public bool ForceRespawn(string trialKey, out string error)
        {
            error = null;

            if (!IsTrialActive(trialKey))
            {
                error = $"Trial '{trialKey}' is not in the current rotation. Active: [{string.Join(", ", GetActiveTrialKeys())}]";
                return false;
            }

            var cfg = FindConfig(trialKey);
            if (cfg == null)
            {
                error = $"No config found for trial '{trialKey}'.";
                return false;
            }

            var existing = entityTracker?.GetTrackedEntity(trialKey);
            if (existing != null && existing.Alive)
            {
                error = $"Trial boss '{trialKey}' is already alive.";
                return false;
            }

            var entry = GetOrCreateEntry(trialKey);
            entry.deadUntilTotalHours = 0;
            stateDirty = true;

            // Try to spawn immediately
            double nowHours = sapi.World.Calendar.TotalHours;
            TrySpawnIfPlayerNearby(cfg, entry, nowHours);

            return true;
        }

        /// <summary>
        /// Get the status of a specific trial boss.
        /// </summary>
        public string GetTrialStatus(string trialKey)
        {
            var cfg = FindConfig(trialKey);
            if (cfg == null) return "unknown";

            var entry = GetOrCreateEntry(trialKey);
            double nowHours = sapi.World?.Calendar?.TotalHours ?? 0;

            var bossEntity = entityTracker?.GetTrackedEntity(trialKey);
            if (bossEntity != null && bossEntity.Alive) return "alive";

            if (entry.deadUntilTotalHours > nowHours)
            {
                double hoursLeft = entry.deadUntilTotalHours - nowHours;
                return $"dead (respawn in {hoursLeft:0.0}h)";
            }

            return "waiting (no player nearby)";
        }

        // ---- Anchor management ----

        /// <summary>
        /// Register an anchor point for a trial boss.
        /// </summary>
        public void SetAnchorPoint(string trialKey, string anchorId, BlockPos pos, float yOffset)
        {
            if (string.IsNullOrWhiteSpace(trialKey) || pos == null) return;

            var cfg = FindConfig(trialKey);
            if (cfg == null)
            {
                sapi?.Logger?.Warning("[HollowTrialSystem] SetAnchorPoint: no config for trialKey '{0}'", trialKey);
                return;
            }

            var entry = GetOrCreateEntry(trialKey);
            entry.anchorPoints ??= new List<HollowTrialAnchorPoint>();

            // Check if anchor already exists
            for (int i = 0; i < entry.anchorPoints.Count; i++)
            {
                if (string.Equals(entry.anchorPoints[i].anchorId, anchorId, StringComparison.OrdinalIgnoreCase))
                {
                    // Update existing
                    entry.anchorPoints[i].x = pos.X;
                    entry.anchorPoints[i].y = pos.Y;
                    entry.anchorPoints[i].z = pos.Z;
                    entry.anchorPoints[i].dim = pos.dimension;
                    entry.anchorPoints[i].yOffset = yOffset;
                    stateDirty = true;
                    return;
                }
            }

            // Add new
            entry.anchorPoints.Add(new HollowTrialAnchorPoint
            {
                anchorId = anchorId,
                x = pos.X,
                y = pos.Y,
                z = pos.Z,
                dim = pos.dimension,
                yOffset = yOffset
            });
            stateDirty = true;

            DebugLog($"Anchor registered: trialKey={trialKey} id={anchorId} pos={pos.X},{pos.Y},{pos.Z} dim={pos.dimension}");
        }

        /// <summary>
        /// Unregister an anchor point for a trial boss.
        /// </summary>
        public void UnsetAnchorPoint(string trialKey, string anchorId, BlockPos pos)
        {
            if (string.IsNullOrWhiteSpace(trialKey) || string.IsNullOrWhiteSpace(anchorId)) return;

            var entry = GetOrCreateEntry(trialKey);
            if (entry?.anchorPoints == null) return;

            for (int i = entry.anchorPoints.Count - 1; i >= 0; i--)
            {
                if (string.Equals(entry.anchorPoints[i].anchorId, anchorId, StringComparison.OrdinalIgnoreCase))
                {
                    entry.anchorPoints.RemoveAt(i);
                    stateDirty = true;

                    // Despawn boss if anchor removed
                    var bossEntity = entityTracker?.GetTrackedEntity(trialKey);
                    if (bossEntity != null && bossEntity.Alive)
                    {
                        sapi.World.DespawnEntity(bossEntity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                    break;
                }
            }
        }

        // ---- Utility ----

        private bool AnyPlayerNear(double x, double y, double z, int dim, float range)
        {
            if (range <= 0) range = 120f;

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

        /// <summary>
        /// Get all known trial keys from loaded configs.
        /// </summary>
        public string[] GetKnownTrialKeys()
        {
            var keys = new List<string>(allConfigs.Count);
            foreach (var cfg in allConfigs)
            {
                if (cfg != null && !string.IsNullOrWhiteSpace(cfg.trialKey))
                    keys.Add(cfg.trialKey);
            }
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            return keys.ToArray();
        }

        /// <summary>
        /// Get the tracked entity for a trial boss (public for tracker action).
        /// </summary>
        public Vintagestory.API.Common.Entities.Entity GetTrackedEntity(string trialKey)
        {
            return entityTracker?.GetTrackedEntity(trialKey);
        }

        /// <summary>
        /// Get the anchor position for a trial boss.
        /// </summary>
        public Vec3d GetAnchorPosition(string trialKey)
        {
            var entry = GetOrCreateEntry(trialKey);
            if (entry?.anchorPoints == null || entry.anchorPoints.Count == 0) return null;

            var anchor = entry.anchorPoints[0];
            return new Vec3d(anchor.x, anchor.y + anchor.yOffset, anchor.z);
        }
    }
}
