using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public partial class HollowTrialSystem
    {
        private TrialReputationManager reputationManager;
        private readonly Dictionary<string, TrialCombatTracker> combatTrackers = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Get the shop network handler (for opening shop from NPC dialogue).
        /// </summary>
        public TrialShopNetworkHandler GetShopHandler() => shopNetworkHandler;

        /// <summary>
        /// Get all loaded trial configs (for anchor boss assignment).
        /// </summary>
        public List<HollowTrialConfig> GetAllConfigs() => allConfigs;

        /// <summary>
        /// Get the currently active weekly modifier.
        /// </summary>
        public int GetActiveModifier() => state?.activeModifier ?? 0;

        /// <summary>
        /// Get total number of unique trial bosses configured (for progress display).
        /// Counts unique bosses × 3 tiers.
        /// </summary>
        public int GetTotalTrialCount() => allConfigs.Count * 3;

        /// <summary>
        /// Find the nearest anchor position from any registered entry.
        /// Used by the tracker to guide players to the nearest rift anchor.
        /// </summary>
        public Vec3d FindNearestAnchorPosition(Vec3d playerPos)
        {
            if (playerPos == null || state?.entries == null) return null;

            Vec3d nearest = null;
            double nearestDistSq = double.MaxValue;

            foreach (var entry in state.entries)
            {
                if (entry?.anchorPoints == null) continue;
                foreach (var ap in entry.anchorPoints)
                {
                    double dx = ap.x + 0.5 - playerPos.X;
                    double dy = ap.y + 0.5 - playerPos.Y;
                    double dz = ap.z + 0.5 - playerPos.Z;
                    double distSq = dx * dx + dy * dy + dz * dz;

                    if (distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        nearest = new Vec3d(ap.x + 0.5, ap.y + ap.yOffset, ap.z + 0.5);
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// Find the nearest unassigned anchor position (for tracker fallback).
        /// </summary>
        public Vec3d FindNearestUnassignedAnchorPosition(Vec3d playerPos)
        {
            return FindNearestAnchorPosition(playerPos);
        }

        /// <summary>
        /// Find the nearest anchor position that matches a specific tier.
        /// Looks up BlockEntity to check tier. If tier is 0, returns any anchor.
        /// </summary>
        public Vec3d FindNearestAnchorPositionByTier(Vec3d playerPos, int tier)
        {
            if (playerPos == null || state?.entries == null || sapi == null) return null;

            Vec3d nearest = null;
            double nearestDistSq = double.MaxValue;

            foreach (var entry in state.entries)
            {
                if (entry?.anchorPoints == null) continue;
                foreach (var ap in entry.anchorPoints)
                {
                    // Check tier by looking up block entity
                    if (tier > 0)
                    {
                        var pos = new BlockPos(ap.x, ap.y, ap.z, ap.dim);
                        var be = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityVoidRiftAnchor;
                        if (be == null || be.Tier != tier) continue;
                    }

                    double dx = ap.x + 0.5 - playerPos.X;
                    double dy = ap.y + 0.5 - playerPos.Y;
                    double dz = ap.z + 0.5 - playerPos.Z;
                    double distSq = dx * dx + dy * dy + dz * dz;

                    if (distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        nearest = new Vec3d(ap.x + 0.5, ap.y + ap.yOffset, ap.z + 0.5);
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// Reload trial configs from disk (hot-reload). Returns number of configs loaded.
        /// Anchor points are preserved — they re-register periodically on their own.
        /// </summary>
        public int ReloadConfigs()
        {
            LoadConfigs();
            return allConfigs.Count;
        }

        /// <summary>
        /// Clears all registered anchor points from state.
        /// Anchors will re-register themselves on their next tick cycle.
        /// </summary>
        public void ClearAllAnchors()
        {
            if (state?.entries == null) return;

            for (int i = 0; i < state.entries.Count; i++)
            {
                var entry = state.entries[i];
                if (entry?.anchorPoints != null)
                {
                    entry.anchorPoints.Clear();
                }
            }

            stateDirty = true;
            sapi?.Logger?.Notification("[HollowTrials] All anchor points cleared. They will re-register on next tick.");
        }

        /// <summary>
        /// Reassign random bosses to all placed VoidRiftAnchor block entities in the world.
        /// Called after rotation/skip. Picks a new random boss for each anchor immediately.
        /// </summary>
        public void ReassignAllAnchors()
        {
            if (sapi == null) return;

            // Collect all anchor positions from state entries
            var anchorPositions = new List<(BlockPos pos, string oldKey)>();
            if (state?.entries != null)
            {
                foreach (var entry in state.entries)
                {
                    if (entry?.anchorPoints == null) continue;
                    foreach (var ap in entry.anchorPoints)
                    {
                        anchorPositions.Add((new BlockPos(ap.x, ap.y, ap.z, ap.dim), entry.trialKey));
                    }
                }
            }

            foreach (var (pos, oldKey) in anchorPositions)
            {
                try
                {
                    var be = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityVoidRiftAnchor;
                    if (be == null) continue;

                    be.ClearAssignedBoss();
                    be.OnBossKilled(0); // Reset cooldown
                    be.AssignRandomBoss(); // Immediately assign new boss
                }
                catch { }
            }

            sapi.Logger.Notification("[HollowTrials] Rotation complete. All anchors reassigned with new bosses.");
        }

        /// <summary>
        /// Get all state entries (for status command to enumerate all anchors).
        /// </summary>
        public List<HollowTrialStateEntry> GetAllEntries()
        {
            return state?.entries;
        }

        /// <summary>
        /// Clear all state entries (anchor registrations, cooldowns, rotation state).
        /// Used by /avq trials clear command.
        /// </summary>
        public void ClearAllEntries()
        {
            if (state != null)
            {
                state.entries?.Clear();
                stateDirty = true;
            }
        }

        /// <summary>
        /// Get friendly IDs of all anchors for a trial key.
        /// </summary>
        public List<string> GetAnchorFriendlyIds(string trialKey)
        {
            var result = new List<string>();
            var entry = GetOrCreateEntry(trialKey);
            if (entry?.anchorPoints == null) return result;

            foreach (var a in entry.anchorPoints)
            {
                if (!string.IsNullOrWhiteSpace(a.friendlyId))
                    result.Add(a.friendlyId);
            }
            return result;
        }

        /// <summary>
        /// Force spawn all active trial bosses at their anchors (tier 1). Used after skip.
        /// Returns number of bosses spawned.
        /// </summary>
        public int ForceSpawnAllActive()
        {
            if (sapi == null || state?.activeTrialKeys == null) return 0;

            int spawned = 0;
            foreach (var trialKey in state.activeTrialKeys)
            {
                var cfg = FindConfig(trialKey);
                if (cfg == null) continue;

                var existing = entityTracker?.GetTrackedEntity(trialKey);
                if (existing != null && existing.Alive) continue;

                var entry = GetOrCreateEntry(trialKey);
                entry.deadUntilTotalHours = 0;

                if (entry.anchorPoints == null || entry.anchorPoints.Count == 0) continue;

                var anchor = entry.anchorPoints[0];
                var point = new Vec3d(anchor.x, anchor.y + anchor.yOffset, anchor.z);
                TrySpawnBoss(cfg, point, anchor.dim, anchor, 1);
                spawned++;
            }

            stateDirty = true;
            return spawned;
        }

        /// <summary>
        /// Get the reputation manager instance.
        /// </summary>
        public TrialReputationManager GetReputationManager()
        {
            if (reputationManager == null && sapi != null)
            {
                reputationManager = new TrialReputationManager(sapi);
            }
            return reputationManager;
        }

        /// <summary>
        /// Get or create a combat tracker for a specific trial boss.
        /// </summary>
        public TrialCombatTracker GetCombatTracker(string trialKey)
        {
            if (string.IsNullOrWhiteSpace(trialKey)) return null;

            if (!combatTrackers.TryGetValue(trialKey, out var tracker))
            {
                tracker = new TrialCombatTracker();
                combatTrackers[trialKey] = tracker;
            }

            return tracker;
        }

        /// <summary>
        /// Find a trial config by its quest ID (searches all tiers of all bosses).
        /// Also returns the tier that matched via out parameter.
        /// </summary>
        public HollowTrialConfig FindConfigByQuestId(string questId)
        {
            return FindConfigByQuestId(questId, out _);
        }

        /// <summary>
        /// Find a trial config by its quest ID, also returning the matched tier.
        /// </summary>
        public HollowTrialConfig FindConfigByQuestId(string questId, out int matchedTier)
        {
            matchedTier = 0;
            if (string.IsNullOrWhiteSpace(questId)) return null;

            for (int i = 0; i < allConfigs.Count; i++)
            {
                var cfg = allConfigs[i];
                if (cfg.tiers == null) continue;

                foreach (var kvp in cfg.tiers)
                {
                    if (string.Equals(kvp.Value?.questId, questId, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedTier = kvp.Key;
                        return cfg;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Save reputation data on world save.
        /// </summary>
        private void SaveReputationData()
        {
            reputationManager?.Save();
        }
    }
}
