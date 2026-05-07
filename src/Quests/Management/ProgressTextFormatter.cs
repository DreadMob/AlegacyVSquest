using VsQuest;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest.Systems.Management
{
    /// <summary>
    /// Service for formatting quest progress text.
    /// Wraps QuestProgressTextUtil for better organization.
    /// </summary>
    public static class ProgressTextFormatter
    {
        /// <summary>
        /// Get formatted progress text for an active quest.
        /// </summary>
        public static string GetActiveQuestText(ICoreServerAPI sapi, IPlayer player, ActiveQuest activeQuest)
        {
            return QuestProgressTextUtil.GetActiveQuestText(sapi, player, activeQuest);
        }
    }
}
