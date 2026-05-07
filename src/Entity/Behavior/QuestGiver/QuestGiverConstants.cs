using System;

namespace VsQuest
{
    /// <summary>
    /// Centralized constants for quest giver functionality.
    /// </summary>
    public static class QuestGiverConstants
    {
        /// <summary>
        /// Attribute key prefix for tracking when a quest was last accepted by a player.
        /// </summary>
        public const string LastAcceptedKeyPrefix = "alegacyvsquest:lastaccepted-";

        /// <summary>
        /// Attribute key prefix for chain cooldown tracking (global cooldown after completing any quest from a quest giver).
        /// </summary>
        public const string ChainCooldownKeyPrefix = "vsquest:questgiver:lastcompleted-";

        /// <summary>
        /// Generates the attribute key for tracking when a specific quest was last accepted.
        /// </summary>
        /// <param name="questId">The quest identifier</param>
        /// <returns>The full attribute key</returns>
        public static string LastAcceptedKey(string questId) => $"{LastAcceptedKeyPrefix}{questId}";

        /// <summary>
        /// Generates the attribute key for chain cooldown tracking for a specific quest giver entity.
        /// </summary>
        /// <param name="questGiverEntityId">The entity ID of the quest giver</param>
        /// <returns>The full attribute key</returns>
        public static string ChainCooldownKey(long questGiverEntityId) => $"{ChainCooldownKeyPrefix}{questGiverEntityId}";

        /// <summary>
        /// Lang key for the "access quests" interaction.
        /// </summary>
        public const string AccessQuestsLangKey = "alegacyvsquest:access-quests";

        /// <summary>
        /// Dialog trigger value for opening the quest selection UI.
        /// </summary>
        public const string DialogTriggerOpenQuests = "openquests";

        /// <summary>
        /// Dialog trigger value for opening the server info UI.
        /// </summary>
        public const string DialogTriggerOpenServerInfo = "openserverinfo";

        /// <summary>
        /// Player attribute key for storing completed quests (legacy format).
        /// </summary>
        public const string PlayerCompletedQuestsKey = "alegacyvsquest:playercompleted";
    }
}
