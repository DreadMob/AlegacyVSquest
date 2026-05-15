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
        /// Get the currently active weekly modifier.
        /// </summary>
        public int GetActiveModifier() => state?.activeModifier ?? 0;

        /// <summary>
        /// Reload trial configs from disk (hot-reload). Returns number of configs loaded.
        /// Also clears all registered anchor points so they re-register on next block tick.
        /// </summary>
        public int ReloadConfigs()
        {
            LoadConfigs();
            ClearAllAnchors();
            return allConfigs.Count;
        }

        /// <summary>
        /// Clears all registered anchor points from state.
        /// Anchors will re-register themselves on their next tick cycle.
        /// </summary>
        public void ClearAllAnchors()
        {
            if (state?.entries == null) return;

            foreach (var kvp in state.entries)
            {
                if (kvp.Value?.anchorPoints != null)
                {
                    kvp.Value.anchorPoints.Clear();
                }
            }

            stateDirty = true;
            sapi?.Logger?.Notification("[HollowTrials] All anchor points cleared. They will re-register on next tick.");
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
