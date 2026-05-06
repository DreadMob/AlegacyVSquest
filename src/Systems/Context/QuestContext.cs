using System.Collections.Generic;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Implementation of IQuestContext that wraps QuestSystem.
    /// Provides safe, null-checked access to quest system components.
    /// </summary>
    public class QuestContext : IQuestContext
    {
        private readonly QuestSystem questSystem;
        private readonly ICoreServerAPI sapi;
        
        public QuestContext(QuestSystem questSystem, ICoreServerAPI sapi)
        {
            this.questSystem = questSystem;
            this.sapi = sapi;
        }
        
        /// <summary>
        /// Get a quest by ID. Returns null if not found.
        /// </summary>
        public Quest GetQuest(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return null;
            if (questSystem?.QuestRegistry == null) return null;
            
            return questSystem.QuestRegistry.TryGetValue(questId, out var quest) ? quest : null;
        }
        
        /// <summary>
        /// Get a quest action by ID. Returns null if not found.
        /// </summary>
        public IQuestAction GetAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId)) return null;
            if (questSystem?.ActionRegistry == null) return null;
            
            return questSystem.ActionRegistry.TryGetValue(actionId, out var action) ? action : null;
        }
        
        /// <summary>
        /// Get an action objective implementation by ID. Returns null if not found.
        /// </summary>
        public ActionObjectiveBase GetObjective(string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(objectiveId)) return null;
            if (questSystem?.ActionObjectiveRegistry == null) return null;
            
            return questSystem.ActionObjectiveRegistry.TryGetValue(objectiveId, out var obj) ? obj : null;
        }
        
        /// <summary>
        /// Check if a quest exists in the registry
        /// </summary>
        public bool QuestExists(string questId)
        {
            return GetQuest(questId) != null;
        }
        
        /// <summary>
        /// Quest system configuration
        /// </summary>
        public QuestConfig Config => questSystem?.Config;
        
        /// <summary>
        /// Core mod configuration
        /// </summary>
        public AlegacyVsQuestConfig CoreConfig => questSystem?.CoreConfig;
        
        /// <summary>
        /// Action objective registry
        /// </summary>
        public Dictionary<string, ActionObjectiveBase> ActionObjectiveRegistry => questSystem?.ActionObjectiveRegistry;
        
        /// <summary>
        /// Action registry
        /// </summary>
        public Dictionary<string, IQuestAction> ActionRegistry => questSystem?.ActionRegistry;
    }
}
