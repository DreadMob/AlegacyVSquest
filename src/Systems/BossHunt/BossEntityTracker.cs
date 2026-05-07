using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Event-driven tracker for boss entities.
    /// Uses server entity events instead of periodic full-world scans.
    /// </summary>
    public class BossEntityTracker : IBossEntityTracker
    {
        private readonly ICoreServerAPI sapi;
        private readonly Dictionary<string, TrackedBoss> trackedBosses = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Fired when a second living entity is detected for a tracked bossKey.
        /// </summary>
        public event Action<string> OnDuplicateBossDetected;

        public BossEntityTracker(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        /// <summary>
        /// Legacy no-op: scan interval is no longer used because scanning is event-driven.
        /// </summary>
        [System.Obsolete("Scan interval is no longer used; tracking is event-driven.")]
        public void SetScanIntervalHours(double hours)
        {
        }

        /// <summary>
        /// Start tracking. Must be called after boss keys are registered.
        /// </summary>
        public void Start()
        {
            if (sapi == null) return;

            sapi.Event.OnEntitySpawn += OnEntitySpawnOrLoaded;
            sapi.Event.OnEntityLoaded += OnEntitySpawnOrLoaded;
            sapi.Event.OnEntityDespawn += OnEntityDespawned;
            sapi.Event.OnEntityDeath += OnEntityDeath;

            DoInitialScan();
        }

        /// <summary>
        /// Stop tracking and cleanup.
        /// </summary>
        public void Stop()
        {
            if (sapi == null) return;

            sapi.Event.OnEntitySpawn -= OnEntitySpawnOrLoaded;
            sapi.Event.OnEntityLoaded -= OnEntitySpawnOrLoaded;
            sapi.Event.OnEntityDespawn -= OnEntityDespawned;
            sapi.Event.OnEntityDeath -= OnEntityDeath;

            trackedBosses.Clear();
        }

        /// <summary>
        /// Register a boss key to track.
        /// </summary>
        /// <param name="bossKey">The boss key to track.</param>
        public void RegisterBossKey(string bossKey)
        {
            if (string.IsNullOrWhiteSpace(bossKey)) return;

            if (!trackedBosses.ContainsKey(bossKey))
            {
                trackedBosses[bossKey] = new TrackedBoss { BossKey = bossKey };
            }
        }

        /// <summary>
        /// Unregister a boss key.
        /// </summary>
        /// <param name="bossKey">The boss key to unregister.</param>
        public void UnregisterBossKey(string bossKey)
        {
            if (string.IsNullOrWhiteSpace(bossKey)) return;

            if (trackedBosses.TryGetValue(bossKey, out var tracked))
            {
                tracked.Entity = null;
                trackedBosses.Remove(bossKey);
            }
        }

        /// <summary>
        /// Get the currently tracked entity for a boss key.
        /// Returns null if not found or not alive.
        /// </summary>
        /// <param name="bossKey">The boss key to look up.</param>
        /// <returns>The tracked entity if alive, null otherwise.</returns>
        public Entity GetTrackedEntity(string bossKey)
        {
            if (string.IsNullOrWhiteSpace(bossKey)) return null;
            if (!trackedBosses.TryGetValue(bossKey, out var tracked)) return null;

            var entity = tracked.Entity;
            if (entity == null || !entity.Alive)
            {
                tracked.Entity = null;
                return null;
            }

            return entity;
        }

        /// <summary>
        /// Get the tracked entity regardless of alive status.
        /// </summary>
        /// <param name="bossKey">The boss key to look up.</param>
        /// <returns>The tracked entity, or null if not found.</returns>
        public Entity GetTrackedEntityAny(string bossKey)
        {
            if (string.IsNullOrWhiteSpace(bossKey)) return null;
            if (!trackedBosses.TryGetValue(bossKey, out var tracked)) return null;

            return tracked.Entity;
        }

        /// <summary>
        /// Get all tracked boss keys.
        /// </summary>
        /// <returns>Array of tracked boss keys, sorted alphabetically.</returns>
        public string[] GetTrackedBossKeys()
        {
            var keys = new List<string>(trackedBosses.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            return keys.ToArray();
        }

        /// <summary>
        /// One-time scan of already-loaded entities at startup.
        /// After this, events keep the tracker up to date.
        /// </summary>
        private void DoInitialScan()
        {
            if (sapi?.World?.LoadedEntities == null) return;

            foreach (var kvp in sapi.World.LoadedEntities)
            {
                TryTrackEntity(kvp.Value);
            }
        }

        /// <summary>
        /// Force re-evaluation by scanning any newly loaded entities.
        /// </summary>
        public void ForceScan()
        {
            DoInitialScan();
        }

        private void OnEntitySpawnOrLoaded(Entity entity)
        {
            TryTrackEntity(entity);
        }

        private void OnEntityDespawned(Entity entity, EntityDespawnData data)
        {
            if (entity == null) return;

            var qt = entity.GetBehavior<EntityBehaviorQuestTarget>();
            if (qt == null || string.IsNullOrWhiteSpace(qt.TargetId)) return;

            if (trackedBosses.TryGetValue(qt.TargetId, out var tracked) && tracked.Entity == entity)
            {
                tracked.Entity = null;
            }
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (entity == null) return;

            var qt = entity.GetBehavior<EntityBehaviorQuestTarget>();
            if (qt == null || string.IsNullOrWhiteSpace(qt.TargetId)) return;

            if (trackedBosses.TryGetValue(qt.TargetId, out var tracked))
            {
                tracked.Entity = null;
            }
        }

        private void TryTrackEntity(Entity entity)
        {
            if (entity == null) return;

            // Skip old-phase entities that are being replaced by rebirth
            if (entity.WatchedAttributes?.GetBool(EntityBehaviorBossRebirth2.RebirthOldPhaseKey, false) == true)
            {
                return;
            }

            var qt = entity.GetBehavior<EntityBehaviorQuestTarget>();
            if (qt == null || string.IsNullOrWhiteSpace(qt.TargetId)) return;

            string bossKey = qt.TargetId;
            if (!trackedBosses.TryGetValue(bossKey, out var tracked)) return;

            // Prefer living entities over corpses
            if (entity.Alive)
            {
                var rebirth = entity.GetBehavior<EntityBehaviorBossRebirth2>();
                if (rebirth != null && !rebirth.IsFinalStage)
                {
                    // Phase 1 boss - keep only if we don't have a better match
                    if (tracked.Entity == null || !tracked.Entity.Alive)
                    {
                        tracked.Entity = entity;
                    }
                }
                else
                {
                    // Final stage or no rebirth - best match
                    if (tracked.Entity != null && tracked.Entity.Alive && tracked.Entity != entity)
                    {
                        OnDuplicateBossDetected?.Invoke(bossKey);
                    }
                    tracked.Entity = entity;
                }
            }
            else if (tracked.Entity == null)
            {
                // Keep corpse as fallback
                tracked.Entity = entity;
            }
        }

        /// <summary>
        /// Internal tracking data for a boss.
        /// </summary>
        private class TrackedBoss
        {
            public string BossKey;
            public Entity Entity;
        }
    }
}
