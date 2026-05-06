using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Utility methods for cleaning up quest objective progress.
    /// </summary>
    public static class QuestObjectiveCleanupUtil
    {
        /// <summary>
        /// Clear kill action target progress when accepting a quest.
        /// Prevents progress from previous quest attempts from carrying over.
        /// </summary>
        public static void ClearKillActionTargetProgressOnAccept(IServerPlayer player, Quest quest)
        {
            if (player?.Entity?.WatchedAttributes == null) return;
            if (quest?.actionObjectives == null) return;

            var wa = player.Entity.WatchedAttributes;

            try
            {
                foreach (var ao in quest.actionObjectives)
                {
                    if (ao == null) continue;
                    if (!string.Equals(ao.id, "killactiontarget", StringComparison.OrdinalIgnoreCase)) continue;
                    if (ao.args == null || ao.args.Length < 2) continue;

                    string questId = ao.args[0];
                    string objectiveId = ao.args[1];
                    if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId)) continue;

                    string key = $"vsquest:killactiontarget:{questId}:{objectiveId}:count";
                    wa.RemoveAttribute(key);
                    wa.MarkPathDirty(key);
                }
            }
            catch (Exception)
            {
                // Failed to clear kill action target progress - non-critical
            }
        }
    }
}
