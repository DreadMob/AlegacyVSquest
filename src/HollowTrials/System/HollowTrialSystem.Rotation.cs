using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public partial class HollowTrialSystem
    {
        private void CheckRotation(double nowHours)
        {
            if (state.nextRotationTotalHours > 0 && nowHours < state.nextRotationTotalHours) return;

            PerformRotation(nowHours);
        }

        private void PerformRotation(double nowHours)
        {
            var previousKeys = new List<string>(state.activeTrialKeys);

            state.activeTrialKeys.Clear();

            // Select one boss per tier
            SelectNextForTier(1, configsByTier1, state.activeTrialKeys);
            SelectNextForTier(2, configsByTier2, state.activeTrialKeys);
            SelectNextForTier(3, configsByTier3, state.activeTrialKeys);

            // Set next rotation time
            double rotationHours = (coreConfig?.RotationDays ?? 60) * 24.0;
            state.nextRotationTotalHours = nowHours + rotationHours;
            stateDirty = true;

            // Despawn old bosses that are no longer active
            foreach (var oldKey in previousKeys)
            {
                if (state.activeTrialKeys.Contains(oldKey)) continue;

                var bossEntity = entityTracker?.GetTrackedEntity(oldKey);
                if (bossEntity != null)
                {
                    try
                    {
                        sapi.World.DespawnEntity(bossEntity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Warning("[HollowTrialSystem] Failed to despawn rotated boss '{0}': {1}", oldKey, ex.Message);
                    }
                }

                // Reset dead timer for rotated-out bosses
                var entry = GetOrCreateEntry(oldKey);
                entry.deadUntilTotalHours = 0;
            }

            // Notify players about rotation
            if (previousKeys.Count > 0 && sapi != null)
            {
                CancelOutdatedTrialQuests(previousKeys);
            }

            DebugLog($"Rotation complete. Active: [{string.Join(", ", state.activeTrialKeys)}]. Next rotation at {state.nextRotationTotalHours:0.0}h");
        }

        private void SelectNextForTier(int tier, List<HollowTrialConfig> tierConfigs, List<string> activeKeys)
        {
            if (tierConfigs == null || tierConfigs.Count == 0)
            {
                sapi?.Logger?.Warning("[HollowTrialSystem] No configs for Tier {0}, skipping.", tier);
                return;
            }

            state.rotationIndexPerTier ??= new Dictionary<int, int>();

            if (!state.rotationIndexPerTier.TryGetValue(tier, out int currentIndex))
            {
                currentIndex = 0;
            }

            // Ensure index is valid
            if (currentIndex < 0 || currentIndex >= tierConfigs.Count)
            {
                currentIndex = 0;
            }

            var selected = tierConfigs[currentIndex];
            activeKeys.Add(selected.trialKey);

            // Advance index for next rotation (cyclic)
            state.rotationIndexPerTier[tier] = (currentIndex + 1) % tierConfigs.Count;
        }

        private void CancelOutdatedTrialQuests(List<string> previousKeys)
        {
            // Cancel quests for bosses that rotated out
            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return;

            foreach (var oldKey in previousKeys)
            {
                if (state.activeTrialKeys.Contains(oldKey)) continue;

                var cfg = FindConfig(oldKey);
                if (cfg == null || string.IsNullOrWhiteSpace(cfg.questId)) continue;

                // Clear cooldowns and reset active quests for this boss
                foreach (var player in sapi.World.AllOnlinePlayers)
                {
                    if (player is not IServerPlayer sp) continue;
                    QuestSystemAdminUtils.ClearQuestCooldownForPlayer(sp, cfg.questId);
                }
            }
        }

        /// <summary>
        /// Force rotation to next set of bosses. Used by admin command.
        /// </summary>
        public bool ForceRotation(out List<string> newActiveKeys)
        {
            newActiveKeys = null;
            if (sapi == null) return false;

            double nowHours = sapi.World.Calendar.TotalHours;
            PerformRotation(nowHours);
            SaveStateIfDirty();

            newActiveKeys = new List<string>(state.activeTrialKeys);
            return true;
        }

        /// <summary>
        /// Get current active trial keys.
        /// </summary>
        public List<string> GetActiveTrialKeys()
        {
            return state?.activeTrialKeys ?? new List<string>();
        }

        /// <summary>
        /// Get hours until next rotation.
        /// </summary>
        public double GetHoursUntilRotation()
        {
            if (sapi?.World?.Calendar == null || state == null) return 0;
            double nowHours = sapi.World.Calendar.TotalHours;
            return state.nextRotationTotalHours > nowHours ? state.nextRotationTotalHours - nowHours : 0;
        }

        /// <summary>
        /// Check if a trial key is currently active.
        /// </summary>
        public bool IsTrialActive(string trialKey)
        {
            if (state?.activeTrialKeys == null) return false;
            return state.activeTrialKeys.Contains(trialKey);
        }
    }
}
