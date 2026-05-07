using System;

namespace VsQuest
{
    /// <summary>
    /// Base exception for all quest-related errors.
    /// </summary>
    public class QuestException : Exception
    {
        /// <summary>
        /// The quest ID related to this exception, if applicable.
        /// </summary>
        public string QuestId { get; }

        /// <summary>
        /// Additional context about where the error occurred.
        /// </summary>
        public string Context { get; }

        public QuestException() { }

        public QuestException(string message) : base(message) { }

        public QuestException(string message, Exception inner) : base(message, inner) { }

        public QuestException(string message, string questId, string context = null)
            : base(message)
        {
            QuestId = questId;
            Context = context;
        }

        public QuestException(string message, string questId, string context, Exception inner)
            : base(message, inner)
        {
            QuestId = questId;
            Context = context;
        }
    }

    /// <summary>
    /// Thrown when a quest is not found in the registry.
    /// </summary>
    public class QuestNotFoundException : QuestException
    {
        public QuestNotFoundException(string questId)
            : base($"Quest '{questId}' not found in registry.", questId, "QuestRegistry") { }

        public QuestNotFoundException(string questId, string context)
            : base($"Quest '{questId}' not found in registry.", questId, context) { }
    }

    /// <summary>
    /// Thrown when a quest has invalid configuration.
    /// </summary>
    public class QuestConfigurationException : QuestException
    {
        public QuestConfigurationException(string questId, string details)
            : base($"Invalid configuration for quest '{questId}': {details}", questId, "Configuration") { }

        public QuestConfigurationException(string questId, string details, Exception inner)
            : base($"Invalid configuration for quest '{questId}': {details}", questId, "Configuration", inner) { }
    }

    /// <summary>
    /// Thrown when a quest giver encounters an error.
    /// </summary>
    public class QuestGiverException : QuestException
    {
        /// <summary>
        /// The entity ID of the quest giver, if applicable.
        /// </summary>
        public long? EntityId { get; }

        public QuestGiverException(long entityId, string message)
            : base($"Quest giver {entityId}: {message}", null, "QuestGiver")
        {
            EntityId = entityId;
        }

        public QuestGiverException(long entityId, string questId, string message)
            : base($"Quest giver {entityId}: {message}", questId, "QuestGiver")
        {
            EntityId = entityId;
        }
    }

    /// <summary>
    /// Thrown when a player is not eligible for a quest.
    /// </summary>
    public class QuestIneligibleException : QuestException
    {
        /// <summary>
        /// The reason for ineligibility.
        /// </summary>
        public string Reason { get; }

        public QuestIneligibleException(string questId, string playerUID, string reason)
            : base($"Player '{playerUID}' is not eligible for quest '{questId}': {reason}", questId, "Eligibility")
        {
            Reason = reason;
        }
    }
}
