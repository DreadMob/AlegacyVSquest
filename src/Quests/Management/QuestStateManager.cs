using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public interface IQuestStateManager
    {
        bool AdvanceStage(ActiveQuest activeQuest, Quest quest);
        bool IsCurrentStageCompletable(ActiveQuest activeQuest, IPlayer byPlayer, Quest quest);
        bool IsCompletable(ActiveQuest activeQuest, IPlayer byPlayer);
        bool AreAllStagesComplete(ActiveQuest activeQuest, IPlayer byPlayer, Quest quest);
        void CompleteQuest(ActiveQuest activeQuest, IPlayer byPlayer);
    }

    public class QuestStateManager : IQuestStateManager
    {
        public bool AdvanceStage(ActiveQuest activeQuest, Quest quest)
        {
            if (activeQuest == null || quest == null) return false;

            // Mark current stage as completed
            if (!activeQuest.completedStageIndices.Contains(activeQuest.currentStageIndex))
            {
                activeQuest.completedStageIndices.Add(activeQuest.currentStageIndex);
            }

            // Check if there's a next stage
            if (activeQuest.currentStageIndex >= quest.StageCount - 1)
            {
                return false; // Already on final stage
            }

            // Advance to next stage
            activeQuest.currentStageIndex++;

            // Reset trackers for new stage
            activeQuest.GetOrCreateTracker().ResetTrackers();

            return true;
        }

        public bool IsCurrentStageCompletable(ActiveQuest activeQuest, IPlayer byPlayer, Quest quest)
        {
            if (activeQuest == null || quest == null || byPlayer == null) 
            {
                byPlayer?.Entity?.Api?.Logger.Debug($"[QuestStateManager] IsCurrentStageCompletable: null check failed");
                return false;
            }
            var stage = activeQuest.GetCurrentStage(quest);
            if (stage == null) 
            {
                byPlayer?.Entity?.Api?.Logger.Debug($"[QuestStateManager] IsCurrentStageCompletable: stage is null");
                return false;
            }
            
            var result = activeQuest.GetOrCreateTracker().CheckObjectivesCompletable(byPlayer, quest, stage, activeQuest.GetQuestContext());
            byPlayer?.Entity?.Api?.Logger.Debug($"[QuestStateManager] IsCurrentStageCompletable for quest {activeQuest.questId} stage {activeQuest.currentStageIndex}: {result}");
            return result;
        }

        public bool IsCompletable(ActiveQuest activeQuest, IPlayer byPlayer)
        {
            if (activeQuest == null || byPlayer == null) 
            {
                byPlayer?.Entity?.Api?.Logger.Debug($"[QuestStateManager] IsCompletable: null check failed");
                return false;
            }
            
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(activeQuest.questId)) 
            {
                byPlayer?.Entity?.Api?.Logger.Debug($"[QuestStateManager] IsCompletable: questSystem or questId check failed");
                return false;
            }
            if (!questSystem.QuestRegistry.TryGetValue(activeQuest.questId, out var quest) || quest == null) 
            {
                byPlayer?.Entity?.Api?.Logger.Debug($"[QuestStateManager] IsCompletable: quest not found in registry: {activeQuest.questId}");
                return false;
            }

            // If quest has stages, check if we're on the final stage and it's complete
            if (quest.HasStages)
            {
                if (activeQuest.currentStageIndex < quest.stages.Count - 1) 
                {
                    byPlayer?.Entity?.Api?.Logger.Debug($"[QuestStateManager] IsCompletable: not on final stage (current: {activeQuest.currentStageIndex}, total: {quest.stages.Count})");
                    return false;
                }
                var result = IsCurrentStageCompletable(activeQuest, byPlayer, quest);
                byPlayer?.Entity?.Api?.Logger.Debug($"[QuestStateManager] IsCompletable for staged quest {activeQuest.questId}: {result}");
                return result;
            }

            // Legacy quest (no stages) - check objectives directly
            var resultLegacy = activeQuest.GetOrCreateTracker().CheckObjectivesCompletable(byPlayer, quest, null, activeQuest.GetQuestContext());
            byPlayer?.Entity?.Api?.Logger.Debug($"[QuestStateManager] IsCompletable for legacy quest {activeQuest.questId}: {resultLegacy}");
            return resultLegacy;
        }

        public bool AreAllStagesComplete(ActiveQuest activeQuest, IPlayer byPlayer, Quest quest)
        {
            if (activeQuest == null || quest == null) return false;
            if (!quest.HasStages) return IsCompletable(activeQuest, byPlayer);

            // On final stage and it's complete
            return activeQuest.currentStageIndex >= quest.stages.Count - 1 && IsCurrentStageCompletable(activeQuest, byPlayer, quest);
        }

        public void CompleteQuest(ActiveQuest activeQuest, IPlayer byPlayer)
        {
            if (activeQuest == null || byPlayer == null) return;
            
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(activeQuest.questId)) return;
            if (!questSystem.QuestRegistry.TryGetValue(activeQuest.questId, out var quest) || quest == null) return;

            var currentStage = quest.GetStage(activeQuest.currentStageIndex);
            var gatherObjectives = currentStage?.gatherObjectives ?? quest.gatherObjectives;
            var blockPlaceObjectives = currentStage?.blockPlaceObjectives ?? quest.blockPlaceObjectives;

            // Hand over gather items
            var tracker = activeQuest.GetOrCreateTracker();
            foreach (var gatherObjective in gatherObjectives)
            {
                tracker.HandOverItems(byPlayer, gatherObjective);
            }
            
            // Remove placed blocks if required
            RemovePlacedBlocks(byPlayer, blockPlaceObjectives, tracker);
        }

        private void RemovePlacedBlocks(IPlayer byPlayer, List<Objective> blockPlaceObjectives, QuestProgressTracker tracker)
        {
            var trackers = tracker.GetTrackers();
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
    }
}
