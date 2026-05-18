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

            // Select N bosses from the pool (cyclic rotation through all bosses)
            int count = Math.Min(coreConfig?.ActiveTrialCount ?? 3, allConfigs.Count);
            SelectNextBosses(count, state.activeTrialKeys);

            // Set next rotation time
            double rotationHours = (coreConfig?.RotationDays ?? 60) * 24.0;
            state.nextRotationTotalHours = nowHours + rotationHours;

            // Roll a new weekly modifier
            state.activeModifier = (int)TrialWeeklyModifierUtils.Roll(new System.Random());

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

                // Broadcast rotation message to all online players (with Discord embed)
                var modType = (TrialModifierType)state.activeModifier;
                string modName = modType != TrialModifierType.None
                    ? LocalizationUtils.GetSafe(TrialWeeklyModifierUtils.GetNameKey(modType))
                    : "";

                string rotationMsg = LocalizationUtils.GetSafe("albase:trial-rotation-reset-chat");
                string discordRotationMsg = modType != TrialModifierType.None
                    ? LocalizationUtils.GetSafe("albase:trial-rotation-discord", "⚡ " + modName)
                    : LocalizationUtils.GetSafe("albase:trial-rotation-discord-nomod");

                GlobalChatBroadcastUtil.BroadcastGeneralChatWithDiscord(
                    sapi, rotationMsg, discordRotationMsg,
                    EnumChatType.Notification, DiscordBroadcastKind.TrialRotation);

                // Broadcast active modifier in-game
                if (modType != TrialModifierType.None)
                {
                    string modMsg = LocalizationUtils.GetSafe("albase:trial-modifier-active", modName);
                    GlobalChatBroadcastUtil.BroadcastGeneralChat(sapi, modMsg, EnumChatType.Notification);
                }
            }

            DebugLog($"Rotation complete. Active: [{string.Join(", ", state.activeTrialKeys)}]. Next rotation at {state.nextRotationTotalHours:0.0}h");

            // Reassign all anchors to new bosses
            ReassignAllAnchors();
        }

        private void SelectNextBosses(int count, List<string> activeKeys)
        {
            if (allConfigs.Count == 0) return;

            state.rotationIndexPerTier ??= new Dictionary<int, int>();

            // Use key 0 for the global rotation index
            if (!state.rotationIndexPerTier.TryGetValue(0, out int currentIndex))
            {
                currentIndex = 0;
            }

            if (currentIndex < 0 || currentIndex >= allConfigs.Count)
            {
                currentIndex = 0;
            }

            for (int i = 0; i < count; i++)
            {
                int idx = (currentIndex + i) % allConfigs.Count;
                activeKeys.Add(allConfigs[idx].trialKey);
            }

            // Advance index for next rotation
            state.rotationIndexPerTier[0] = (currentIndex + count) % allConfigs.Count;
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
                if (cfg == null) continue;

                // Cancel quests for all tiers of this boss
                foreach (var tierKvp in cfg.tiers)
                {
                    string questId = tierKvp.Value?.questId;
                    if (string.IsNullOrWhiteSpace(questId)) continue;

                    foreach (var player in sapi.World.AllOnlinePlayers)
                    {
                        if (player is not IServerPlayer sp) continue;
                        QuestSystemAdminUtils.ClearQuestCooldownForPlayer(sp, questId);
                    }
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
