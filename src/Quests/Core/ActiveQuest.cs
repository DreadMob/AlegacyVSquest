using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Represents an active quest with progress tracking.
    /// This class stores quest data and delegates tracking to QuestProgressTracker.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ActiveQuest
    {
        // Serialized quest data
        public long questGiverId { get; set; }
        public string questId { get; set; }
        public int currentStageIndex { get; set; } = 0;
        public List<int> completedStageIndices { get; set; } = new List<int>();
        
        // Serialized tracker progress (for persistence between sessions)
        public List<int> trackerProgressData { get; set; } = new List<int>();

        // Client-side UI state (separated into its own class)
        public ActiveQuestClientState ClientState { get; set; } = new ActiveQuestClientState();

        // Progress tracker (not serialized - created on demand)
        [ProtoIgnore]
        private QuestProgressTracker _progressTracker;

        [ProtoIgnore]
        internal IQuestContext _questContext;

        [ProtoIgnore]
        private bool _trackersInitialized = false;

        [ProtoIgnore]
        private IQuestStateManager stateManager;

        /// <summary>
        /// Get or create the progress tracker for this quest.
        /// Auto-initializes if context is available.
        /// </summary>
        internal QuestProgressTracker GetOrCreateTracker()
        {
            if (_progressTracker == null)
            {
                _progressTracker = new QuestProgressTracker(questId);
                
                // Auto-initialize if context is set
                if (_questContext != null)
                {
                    _progressTracker.InitializeTrackers(_questContext, currentStageIndex);
                }
            }
            return _progressTracker;
        }
        
        /// <summary>
        /// Ensure tracker is initialized with context (call from events).
        /// </summary>
        internal void EnsureInitialized(IPlayer byPlayer)
        {
            if (_questContext == null && byPlayer != null)
            {
                var sapi = byPlayer.Entity?.Api as ICoreServerAPI;
                var qs = byPlayer.Entity?.Api?.ModLoader?.GetModSystem<QuestSystem>();
                if (qs != null && sapi != null)
                {
                    _questContext = new QuestContext(qs, sapi);
                    GetOrCreateTracker().InitializeTrackers(_questContext, currentStageIndex);
                }
            }
        }

        /// <summary>
        /// Set the state manager for this quest (dependency injection).
        /// </summary>
        internal void SetStateManager(IQuestStateManager manager)
        {
            stateManager = manager;
        }

        /// <summary>
        /// Initialize trackers from quest objectives. Called when quest is accepted or stage advances.
        /// </summary>
        /// <param name="context">Quest context for lookups.</param>
        public void InitializeTrackers(IQuestContext context)
        {
            _questContext = context;
            GetOrCreateTracker().InitializeTrackers(context, currentStageIndex);
            
            // Restore progress from serialized data if available
            if (!_trackersInitialized && trackerProgressData?.Count > 0)
            {
                GetOrCreateTracker().RestoreProgress(trackerProgressData);
            }
            _trackersInitialized = true;
        }
        
        /// <summary>
        /// Export current tracker progress for serialization. Call before saving.
        /// </summary>
        public void ExportProgress()
        {
            if (_progressTracker != null)
            {
                trackerProgressData = _progressTracker.ExportProgress();
            }
        }
        
        /// <summary>
        /// Get all objective trackers (thread-safe copy).
        /// </summary>
        public IReadOnlyList<IObjectiveTracker> GetTrackers() => GetOrCreateTracker().GetTrackers();

        /// <summary>
        /// Get the quest context for this quest.
        /// </summary>
        internal IQuestContext GetQuestContext() => _questContext;
        
        /// <summary>
        /// Called when a block is used (interacted with). Delegates to event dispatcher.
        /// </summary>
        public void OnBlockUsed(string blockCode, int[] position, IPlayer byPlayer, ICoreServerAPI sapi, IQuestContext context)
        {
            if (string.IsNullOrWhiteSpace(blockCode) || byPlayer == null || sapi == null) return;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return;

            questSystem.DispatchBlockUsedEvent(this, blockCode, position, byPlayer, context);
        }

        /// <summary>
        /// Helper method to get quest and stage from player context.
        /// </summary>
        private bool TryGetQuestAndStage(IPlayer byPlayer, out QuestSystem questSystem, out Quest quest, out QuestStage stage)
        {
            questSystem = byPlayer?.Entity?.Api?.ModLoader?.GetModSystem<QuestSystem>();
            quest = null;
            stage = null;

            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return false;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out quest) || quest == null) return false;

            stage = quest.GetStage(currentStageIndex);
            return true;
        }

        /// <summary>
        /// Gets the current stage for this quest. Returns null if quest not found.
        /// </summary>
        public QuestStage GetCurrentStage(Quest quest)
        {
            if (quest == null) return null;
            return quest.GetStage(currentStageIndex);
        }

        /// <summary>
        /// Checks if the current stage is completable (not the entire quest)
        /// </summary>
        public bool IsCurrentStageCompletable(IPlayer byPlayer, Quest quest)
        {
            if (quest == null || byPlayer == null) return false;
            var stage = GetCurrentStage(quest);
            if (stage == null) return false;
            
            return GetOrCreateTracker().CheckObjectivesCompletable(byPlayer, quest, stage, _questContext);
        }

        /// <summary>
        /// Checks if the entire quest is completable (all stages complete)
        /// </summary>
        public bool IsCompletable(IPlayer byPlayer)
        {
            return stateManager?.IsCompletable(this, byPlayer) ?? false;
        }

        /// <summary>
        /// Advances to the next stage. Returns true if advanced, false if already on final stage.
        /// </summary>
        public bool AdvanceStage(Quest quest)
        {
            return stateManager?.AdvanceStage(this, quest) ?? false;
        }

        /// <summary>
        /// Returns true if all stages are complete (quest can be turned in)
        /// </summary>
        public bool AreAllStagesComplete(IPlayer byPlayer, Quest quest)
        {
            return stateManager?.AreAllStagesComplete(this, byPlayer, quest) ?? false;
        }

        /// <summary>
        /// Complete the quest - hand over items and remove placed blocks if needed.
        /// </summary>
        public void CompleteQuest(IPlayer byPlayer)
        {
            stateManager?.CompleteQuest(this, byPlayer);
        }

        /// <summary>
        /// Get progress of all objective trackers (excluding action objectives).
        /// </summary>
        /// <returns>List of progress values for each tracker.</returns>
        public List<int> trackerProgress() => GetOrCreateTracker().GetTrackerProgress();

        /// <summary>
        /// Get progress of all gather objectives.
        /// </summary>
        /// <param name="byPlayer">The player to check inventory for.</param>
        /// <returns>List of item counts for each gather objective.</returns>
        public List<int> gatherProgress(IPlayer byPlayer)
        {
            if (!TryGetQuestAndStage(byPlayer, out _, out var quest, out var currentStage)) return new List<int>();
            var gatherObjectives = currentStage?.gatherObjectives ?? quest.gatherObjectives;
            return GetOrCreateTracker().GetGatherProgress(byPlayer, gatherObjectives);
        }

        /// <summary>
        /// Get total progress including gather, tracker, and action objectives.
        /// </summary>
        /// <param name="byPlayer">The player to check progress for.</param>
        /// <returns>Combined list of all progress values.</returns>
        public List<int> GetProgress(IPlayer byPlayer)
        {
            if (!TryGetQuestAndStage(byPlayer, out var questSystem, out var quest, out var currentStage)) return new List<int>();

            var actionObjectives = currentStage?.actionObjectives ?? quest.actionObjectives;
            var activeActionObjectives = actionObjectives.ConvertAll<ActionObjectiveBase>(objective => questSystem.ActionObjectiveRegistry[objective.id]);
            var gatherObjectives = currentStage?.gatherObjectives ?? quest.gatherObjectives;

            var result = new List<int>();
            
            // Gather progress
            result.AddRange(GetOrCreateTracker().GetGatherProgress(byPlayer, gatherObjectives));
            
            // Tracker progress
            result.AddRange(GetOrCreateTracker().GetTrackerProgress());
            
            // Action objective progress
            for (int i = 0; i < activeActionObjectives.Count; i++)
            {
                result.AddRange(activeActionObjectives[i].GetProgress(byPlayer, actionObjectives[i].args));
            }

            return result;
        }
    }
}