using Vintagestory.API.Common;

namespace VsQuest
{
    /// <summary>
    /// Context interface for quest-related lookups.
    /// Provides abstraction over QuestSystem to reduce coupling and improve testability.
    /// </summary>
    public interface IQuestContext
    {
        /// <summary>
        /// Get a quest by ID
        /// </summary>
        Quest GetQuest(string questId);
        
        /// <summary>
        /// Get a quest action by ID
        /// </summary>
        IQuestAction GetAction(string actionId);
        
        /// <summary>
        /// Get an action objective implementation by ID
        /// </summary>
        ActionObjectiveBase GetObjective(string objectiveId);
        
        /// <summary>
        /// Check if quest exists
        /// </summary>
        bool QuestExists(string questId);
        
        /// <summary>
        /// Quest system configuration
        /// </summary>
        QuestConfig Config { get; }
        
        /// <summary>
        /// Core mod configuration
        /// </summary>
        AlegacyVsQuestConfig CoreConfig { get; }
        
        /// <summary>
        /// Action objective registry
        /// </summary>
        System.Collections.Generic.Dictionary<string, ActionObjectiveBase> ActionObjectiveRegistry { get; }
        
        /// <summary>
        /// Action registry
        /// </summary>
        System.Collections.Generic.Dictionary<string, IQuestAction> ActionRegistry { get; }
    }
}
