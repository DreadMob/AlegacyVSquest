using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Quest onAccepted action: resets all trial combat trackers so that
    /// a previously killed boss doesn't instantly complete the new quest.
    /// Usage in quest JSON: { "id": "resettrialtracker", "args": [] }
    /// </summary>
    public class ResetTrialTrackersAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            if (sapi == null) return;

            var trialSystem = sapi.ModLoader.GetModSystem<HollowTrialSystem>();
            if (trialSystem == null) return;

            // Reset ALL trial trackers (not just active keys) to prevent stale data
            var allConfigs = trialSystem.GetAllConfigs();
            if (allConfigs == null || allConfigs.Count == 0) return;

            foreach (var cfg in allConfigs)
            {
                if (cfg == null || string.IsNullOrWhiteSpace(cfg.trialKey)) continue;
                var tracker = trialSystem.GetCombatTracker(cfg.trialKey);
                if (tracker != null && tracker.IsFinished)
                {
                    tracker.Reset();
                }
            }
        }
    }
}
