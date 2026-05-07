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
        
        // Client-side UI state
        public bool IsCompletableOnClient { get; set; }
        public bool IsCurrentStageCompleteOnClient { get; set; }
        public string ProgressText { get; set; }
        
        // Progress tracker (not serialized - created on demand)
        [ProtoIgnore]
        private QuestProgressTracker _progressTracker;
        
        [ProtoIgnore]
        private IQuestContext _questContext;
        
        [ProtoIgnore]
        private bool _trackersInitialized = false;

        // Interact debounce cache
        private const int LastInteractDebounceMs = 100;

        private class LastInteractCache
        {
            public long LastWriteMs;
            public int X;
            public int Y;
            public int Z;
            public int Dim;
        }

        private static readonly SimpleLRUCache<string, LastInteractCache> lastInteractCacheByPlayerUid = 
            new SimpleLRUCache<string, LastInteractCache>(1000, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Get or create the progress tracker for this quest.
        /// Auto-initializes if context is available.
        /// </summary>
        private QuestProgressTracker GetOrCreateTracker()
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
        private void EnsureInitialized(IPlayer byPlayer)
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
        /// Called when an entity is killed. Updates kill objective trackers.
        /// </summary>
        /// <param name="entityCode">The code of the killed entity.</param>
        /// <param name="byPlayer">The player who killed the entity.</param>
        /// <param name="context">Quest context for lookups.</param>
        public void OnEntityKilled(string entityCode, IPlayer byPlayer, IQuestContext context)
        {
            if (string.IsNullOrWhiteSpace(entityCode) || byPlayer == null) return;
            EnsureInitialized(byPlayer);
            GetOrCreateTracker().OnEntityKilled(entityCode, byPlayer, context ?? _questContext);
        }

        /// <summary>
        /// Called when a block is placed. Updates block place objective trackers.
        /// </summary>
        /// <param name="blockCode">The code of the placed block.</param>
        /// <param name="position">The position where the block was placed.</param>
        /// <param name="byPlayer">The player who placed the block.</param>
        /// <param name="context">Quest context for lookups.</param>
        public void OnBlockPlaced(string blockCode, int[] position, IPlayer byPlayer, IQuestContext context)
        {
            if (string.IsNullOrWhiteSpace(blockCode) || byPlayer == null) return;
            EnsureInitialized(byPlayer);
            GetOrCreateTracker().OnBlockPlaced(blockCode, position, byPlayer, context ?? _questContext);
        }

        /// <summary>
        /// Called when a block is broken. Updates block break objective trackers.
        /// </summary>
        /// <param name="blockCode">The code of the broken block.</param>
        /// <param name="position">The position where the block was broken.</param>
        /// <param name="byPlayer">The player who broke the block.</param>
        /// <param name="context">Quest context for lookups.</param>
        public void OnBlockBroken(string blockCode, int[] position, IPlayer byPlayer, IQuestContext context)
        {
            if (string.IsNullOrWhiteSpace(blockCode) || byPlayer == null) return;
            EnsureInitialized(byPlayer);
            GetOrCreateTracker().OnBlockBroken(blockCode, position, byPlayer, context ?? _questContext);
        }

        /// <summary>
        /// Called when a block is used (interacted with). Updates interact objective trackers.
        /// </summary>
        /// <param name="blockCode">The code of the used block.</param>
        /// <param name="position">The position of the block.</param>
        /// <param name="byPlayer">The player who used the block.</param>
        /// <param name="sapi">Server API for logging and actions.</param>
        /// <param name="context">Quest context for lookups.</param>
        public void OnBlockUsed(string blockCode, int[] position, IPlayer byPlayer, ICoreServerAPI sapi, IQuestContext context)
        {
            if (string.IsNullOrWhiteSpace(blockCode) || byPlayer == null) return;
            
            var ctx = context ?? _questContext;
            if (ctx == null) return;
            
            var quest = ctx.GetQuest(questId);
            if (quest == null) return;

            // Debounce last interact position tracking
            UpdateLastInteractPosition(byPlayer, position, sapi);

            // Delegate tracking to QuestProgressTracker
            GetOrCreateTracker().OnBlockUsed(blockCode, position, byPlayer, ctx);
            
            // Handle action rewards for interact objectives
            HandleInteractRewards(quest, blockCode, position, byPlayer as IServerPlayer, ctx, sapi);
        }
        
        /// <summary>
        /// Update last interact position with debouncing.
        /// </summary>
        private void UpdateLastInteractPosition(IPlayer byPlayer, int[] position, ICoreServerAPI sapi)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null || position == null || position.Length != 3) return;
            
            var wa = byPlayer.Entity.WatchedAttributes;
            int x = position[0], y = position[1], z = position[2];
            int dim = byPlayer.Entity?.Pos?.Dimension ?? 0;

            try
            {
                string uid = byPlayer.PlayerUID;
                if (string.IsNullOrWhiteSpace(uid)) return;

                if (!lastInteractCacheByPlayerUid.TryGetValue(uid, out var cache) || cache == null)
                {
                    cache = new LastInteractCache { X = int.MinValue, Y = int.MinValue, Z = int.MinValue, Dim = int.MinValue };
                    lastInteractCacheByPlayerUid.Add(uid, cache);
                }

                long now = Environment.TickCount64;
                if ((now - cache.LastWriteMs) >= LastInteractDebounceMs || cache.X != x || cache.Y != y || cache.Z != z || cache.Dim != dim)
                {
                    cache.LastWriteMs = now;
                    cache.X = x; cache.Y = y; cache.Z = z; cache.Dim = dim;
                    
                    wa.SetInt("alegacyvsquest:lastinteract:x", x);
                    wa.SetInt("alegacyvsquest:lastinteract:y", y);
                    wa.SetInt("alegacyvsquest:lastinteract:z", z);
                    wa.SetInt("alegacyvsquest:lastinteract:dim", dim);
                }
            }
            catch (Exception ex)
            {
                sapi?.Logger.Warning("[ActiveQuest] Failed to update last interact cache: {0}", ex.Message);
            }
        }
        
        /// <summary>
        /// Handle action rewards for interact objectives.
        /// </summary>
        private void HandleInteractRewards(Quest quest, string blockCode, int[] position, IServerPlayer serverPlayer, IQuestContext ctx, ICoreServerAPI sapi)
        {
            if (serverPlayer == null) return;
            
            var currentStage = quest.GetStage(currentStageIndex);
            var interactObjectives = currentStage?.interactObjectives ?? quest.interactObjectives;
            
            Systems.Interaction.InteractionService.TryHandleInteractAtObjectives(quest, this, serverPlayer, position, sapi);
            
            foreach (var objective in interactObjectives)
            {
                if (!QuestObjectiveMatchUtil.InteractObjectiveMatches(objective, blockCode, position)) continue;

                var message = new QuestAcceptedMessage { questGiverId = questGiverId, questId = questId };
                foreach (var actionReward in objective.actionRewards)
                {
                    var action = ctx.GetAction(actionReward.id);
                    action?.Execute(sapi, message, serverPlayer, actionReward.args);
                }
            }
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
            if (byPlayer == null) return false;
            
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return false;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return false;

            // If quest has stages, check if we're on the final stage and it's complete
            if (quest.HasStages)
            {
                if (currentStageIndex < quest.stages.Count - 1) return false;
                return IsCurrentStageCompletable(byPlayer, quest);
            }

            // Legacy quest (no stages) - check objectives directly
            return GetOrCreateTracker().CheckObjectivesCompletable(byPlayer, quest, null, _questContext);
        }

        /// <summary>
        /// Advances to the next stage. Returns true if advanced, false if already on final stage.
        /// </summary>
        public bool AdvanceStage(Quest quest)
        {
            if (quest == null) return false;

            // Mark current stage as completed
            if (!completedStageIndices.Contains(currentStageIndex))
            {
                completedStageIndices.Add(currentStageIndex);
            }

            // Check if there's a next stage
            if (currentStageIndex >= quest.StageCount - 1)
            {
                return false; // Already on final stage
            }

            // Advance to next stage
            currentStageIndex++;

            // Reset trackers for new stage
            GetOrCreateTracker().ResetTrackers();

            return true;
        }

        /// <summary>
        /// Returns true if all stages are complete (quest can be turned in)
        /// </summary>
        public bool AreAllStagesComplete(IPlayer byPlayer, Quest quest)
        {
            if (quest == null) return false;
            if (!quest.HasStages) return IsCompletable(byPlayer);

            // On final stage and it's complete
            return currentStageIndex >= quest.stages.Count - 1 && IsCurrentStageCompletable(byPlayer, quest);
        }

        /// <summary>
        /// Complete the quest - hand over items and remove placed blocks if needed.
        /// </summary>
        public void CompleteQuest(IPlayer byPlayer)
        {
            if (byPlayer == null) return;
            
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return;

            var currentStage = quest.GetStage(currentStageIndex);
            var gatherObjectives = currentStage?.gatherObjectives ?? quest.gatherObjectives;
            var blockPlaceObjectives = currentStage?.blockPlaceObjectives ?? quest.blockPlaceObjectives;

            // Hand over gather items
            var tracker = GetOrCreateTracker();
            foreach (var gatherObjective in gatherObjectives)
            {
                tracker.HandOverItems(byPlayer, gatherObjective);
            }
            
            // Remove placed blocks if required
            RemovePlacedBlocks(byPlayer, blockPlaceObjectives);
        }
        
        /// <summary>
        /// Remove placed blocks for block place objectives that have removeAfterFinished=true.
        /// </summary>
        private void RemovePlacedBlocks(IPlayer byPlayer, List<Objective> blockPlaceObjectives)
        {
            var trackers = GetOrCreateTracker().GetTrackers();
            int trackerIndex = 0;
            
            for (int i = 0; i < blockPlaceObjectives.Count && trackerIndex < trackers.Count; i++, trackerIndex++)
            {
                if (blockPlaceObjectives[i].removeAfterFinished && trackers[trackerIndex] is BlockObjectiveTracker bt)
                {
                    int maxRemovals = 100;
                    int removed = 0;
                    foreach (var pos in bt.PlacedPositions)
                    {
                        if (++removed > maxRemovals) break;
                        var ba = byPlayer.Entity.World.BlockAccessor;
                        var blockPos = new BlockPos(pos.X, pos.Y, pos.Z, 0);
                        ba.RemoveBlockEntity(blockPos);
                        ba.SetBlock(0, blockPos);
                    }
                }
            }
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