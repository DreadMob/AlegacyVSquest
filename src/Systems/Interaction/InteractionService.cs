using VsQuest;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest.Systems.Interaction
{
    /// <summary>
    /// Service for handling interaction-related quest objectives.
    /// Wraps QuestInteractAtUtil for better organization.
    /// </summary>
    public static class InteractionService
    {
        /// <summary>
        /// Try to parse a position string into coordinates.
        /// </summary>
        public static bool TryParsePos(string posStr, out int x, out int y, out int z)
        {
            return QuestInteractAtUtil.TryParsePos(posStr, out x, out y, out z);
        }

        /// <summary>
        /// Generate a unique interaction key for a position.
        /// </summary>
        public static string InteractionKey(int x, int y, int z)
        {
            return QuestInteractAtUtil.InteractionKey(x, y, z);
        }

        /// <summary>
        /// Check if player has completed interaction at a position.
        /// </summary>
        public static bool HasInteraction(IPlayer player, int x, int y, int z)
        {
            return QuestInteractAtUtil.HasInteraction(player, x, y, z);
        }

        /// <summary>
        /// Count completed interactions for coordinate arguments.
        /// </summary>
        public static int CountCompleted(IPlayer player, string[] coordArgs)
        {
            return QuestInteractAtUtil.CountCompleted(player, coordArgs);
        }

        /// <summary>
        /// Handle interact-at objectives for a quest.
        /// </summary>
        public static void TryHandleInteractAtObjectives(Quest quest, ActiveQuest activeQuest, IServerPlayer player, int[] position, ICoreServerAPI sapi)
        {
            QuestInteractAtUtil.TryHandleInteractAtObjectives(quest, activeQuest, player, position, sapi);
        }

        /// <summary>
        /// Reset completed interact-at objectives for a quest.
        /// </summary>
        public static void ResetCompletedInteractAtObjectives(Quest quest, IServerPlayer player)
        {
            QuestInteractAtUtil.ResetCompletedInteractAtObjectives(quest, player);
        }
    }
}
