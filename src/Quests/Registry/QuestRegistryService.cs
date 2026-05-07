using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    /// <summary>
    /// Instance-based registry service implementing IQuestRegistryService.
    /// Use QuestRegistryService.Instance for backward-compatible static access.
    /// </summary>
    public class QuestRegistryService : IQuestRegistryService
    {
        private static QuestRegistryService _instance;
        
        /// <summary>
        /// Backward-compatible static instance. Prefer dependency injection for new code.
        /// </summary>
        public static QuestRegistryService Instance => _instance ??= new QuestRegistryService();
        
        // Backward-compatible static accessors
        public static Dictionary<string, Quest> QuestRegistry => Instance.QuestRegistryInstance;
        public static Dictionary<string, IQuestAction> ActionRegistry => Instance.ActionRegistryInstance;
        public static Dictionary<string, ActionObjectiveBase> ActionObjectiveRegistry => Instance.ActionObjectiveRegistryInstance;

        public Dictionary<string, Quest> QuestRegistryInstance { get; private set; } = new Dictionary<string, Quest>();
        public Dictionary<string, IQuestAction> ActionRegistryInstance { get; private set; } = new Dictionary<string, IQuestAction>();
        public Dictionary<string, ActionObjectiveBase> ActionObjectiveRegistryInstance { get; private set; } = new Dictionary<string, ActionObjectiveBase>();

        // Explicit interface implementations
        Dictionary<string, Quest> IQuestRegistryService.QuestRegistry => QuestRegistryInstance;
        Dictionary<string, IQuestAction> IQuestRegistryService.ActionRegistry => ActionRegistryInstance;
        Dictionary<string, ActionObjectiveBase> IQuestRegistryService.ActionObjectiveRegistry => ActionObjectiveRegistryInstance;

        private ICoreAPI api;

        /// <summary>
        /// Initialize the registry service with the API reference.
        /// </summary>
        public static void Initialize(ICoreAPI api)
        {
            Instance.api = api;
        }

        /// <summary>
        /// Register a quest definition.
        /// </summary>
        public static void RegisterQuest(Quest quest, string source)
        {
            Instance.RegisterQuestInstance(quest, source);
        }

        public void RegisterQuestInstance(Quest quest, string source)
        {
            if (quest == null) return;
            if (string.IsNullOrWhiteSpace(quest.id)) return;

            if (QuestRegistryInstance.ContainsKey(quest.id)) return;

            QuestRegistryInstance.Add(quest.id, quest);
        }

        /// <summary>
        /// Register a quest action.
        /// </summary>
        public static void RegisterAction(string id, IQuestAction action)
        {
            Instance.RegisterActionInstance(id, action);
        }

        public void RegisterActionInstance(string id, IQuestAction action)
        {
            if (string.IsNullOrWhiteSpace(id) || action == null) return;
            
            if (ActionRegistryInstance.ContainsKey(id)) return;

            ActionRegistryInstance.Add(id, action);
        }

        /// <summary>
        /// Register an action objective.
        /// </summary>
        public static void RegisterObjective(string id, ActionObjectiveBase objective)
        {
            Instance.RegisterObjectiveInstance(id, objective);
        }

        public void RegisterObjectiveInstance(string id, ActionObjectiveBase objective)
        {
            if (string.IsNullOrWhiteSpace(id) || objective == null) return;

            if (ActionObjectiveRegistryInstance.ContainsKey(id)) return;

            ActionObjectiveRegistryInstance.Add(id, objective);
        }

        /// <summary>
        /// Clear all registries (useful for asset reloads).
        /// </summary>
        public static void ClearAll()
        {
            Instance.ClearAllInstance();
        }

        public void ClearAllInstance()
        {
            QuestRegistryInstance.Clear();
            ActionRegistryInstance.Clear();
            ActionObjectiveRegistryInstance.Clear();
        }
    }
}
