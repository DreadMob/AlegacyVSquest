using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    /// <summary>
    /// Central registry service for quest definitions, actions, and objectives.
    /// Provides static access to registries across the codebase.
    /// </summary>
    public static class QuestRegistryService
    {
        public static Dictionary<string, Quest> QuestRegistry { get; private set; } = new Dictionary<string, Quest>();
        public static Dictionary<string, IQuestAction> ActionRegistry { get; private set; } = new Dictionary<string, IQuestAction>();
        public static Dictionary<string, ActionObjectiveBase> ActionObjectiveRegistry { get; private set; } = new Dictionary<string, ActionObjectiveBase>();

        private static ICoreAPI api;

        /// <summary>
        /// Initialize the registry service with the API reference.
        /// </summary>
        public static void Initialize(ICoreAPI api)
        {
            QuestRegistryService.api = api;
        }

        /// <summary>
        /// Register a quest definition.
        /// </summary>
        public static void RegisterQuest(Quest quest, string source)
        {
            if (quest == null) return;
            if (string.IsNullOrWhiteSpace(quest.id)) return;

            if (QuestRegistry.ContainsKey(quest.id)) return;

            QuestRegistry.Add(quest.id, quest);
        }

        /// <summary>
        /// Register a quest action.
        /// </summary>
        public static void RegisterAction(string id, IQuestAction action)
        {
            if (string.IsNullOrWhiteSpace(id) || action == null) return;
            
            if (ActionRegistry.ContainsKey(id)) return;

            ActionRegistry.Add(id, action);
        }

        /// <summary>
        /// Register an action objective.
        /// </summary>
        public static void RegisterObjective(string id, ActionObjectiveBase objective)
        {
            if (string.IsNullOrWhiteSpace(id) || objective == null) return;

            if (ActionObjectiveRegistry.ContainsKey(id)) return;

            ActionObjectiveRegistry.Add(id, objective);
        }

        /// <summary>
        /// Clear all registries (useful for asset reloads).
        /// </summary>
        public static void ClearAll()
        {
            QuestRegistry.Clear();
            ActionRegistry.Clear();
            ActionObjectiveRegistry.Clear();
        }
    }
}
