using System;
using System.Collections.Generic;

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
        /// Find a trial config by its quest ID.
        /// </summary>
        public HollowTrialConfig FindConfigByQuestId(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return null;

            for (int i = 0; i < allConfigs.Count; i++)
            {
                if (string.Equals(allConfigs[i].questId, questId, StringComparison.OrdinalIgnoreCase))
                    return allConfigs[i];
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
