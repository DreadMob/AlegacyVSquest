using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class ActiveQuestEventDispatcher : IQuestEventDispatcher
    {
        private readonly ICoreServerAPI _sapi;
        private readonly IInteractPositionCache _positionCache;

        public ActiveQuestEventDispatcher(ICoreServerAPI sapi, IInteractPositionCache positionCache)
        {
            _sapi = sapi;
            _positionCache = positionCache;
        }

        public void OnEntityKilled(ActiveQuest activeQuest, string entityCode, IPlayer byPlayer, IQuestContext context)
        {
            if (activeQuest == null || byPlayer == null) return;
            
            activeQuest.EnsureInitialized(byPlayer);
            activeQuest.GetOrCreateTracker().OnEntityKilled(entityCode, byPlayer, context ?? activeQuest.GetQuestContext());
        }

        public void OnBlockPlaced(ActiveQuest activeQuest, string blockCode, int[] position, IPlayer byPlayer, IQuestContext context)
        {
            if (activeQuest == null || byPlayer == null) return;
            
            activeQuest.EnsureInitialized(byPlayer);
            activeQuest.GetOrCreateTracker().OnBlockPlaced(blockCode, position, byPlayer, context ?? activeQuest.GetQuestContext());
        }

        public void OnBlockBroken(ActiveQuest activeQuest, string blockCode, int[] position, IPlayer byPlayer, IQuestContext context)
        {
            if (activeQuest == null || byPlayer == null) return;
            
            activeQuest.EnsureInitialized(byPlayer);
            activeQuest.GetOrCreateTracker().OnBlockBroken(blockCode, position, byPlayer, context ?? activeQuest.GetQuestContext());
        }

        public void OnBlockUsed(ActiveQuest activeQuest, string blockCode, int[] position, IPlayer byPlayer, IQuestContext context)
        {
            if (activeQuest == null || string.IsNullOrWhiteSpace(blockCode) || byPlayer == null) return;

            var ctx = context ?? activeQuest.GetQuestContext();
            if (ctx == null) return;

            try
            {
                _positionCache.UpdatePosition(activeQuest.questId, position);
            }
            catch (Exception ex)
            {
                _sapi?.Logger.Warning("[ActiveQuestEventDispatcher] Failed to update last interact cache: {0}", ex.Message);
            }

            var quest = ctx.GetQuest(activeQuest.questId);
            if (quest == null) return;

            var serverPlayer = byPlayer as IServerPlayer;
            if (serverPlayer != null)
            {
                HandleInteractRewards(quest, activeQuest, blockCode, position, serverPlayer, ctx);
            }
        }

        private void UpdateLastInteractPosition(IPlayer byPlayer, int[] position)
        {
            try
            {
                _positionCache.UpdatePosition(byPlayer?.PlayerUID ?? "unknown", position);
            }
            catch (Exception ex)
            {
                _sapi?.Logger.Warning("[ActiveQuestEventDispatcher] Failed to update last interact cache: {0}", ex.Message);
            }
        }

        private void HandleInteractRewards(Quest quest, ActiveQuest activeQuest, string blockCode, int[] position, IServerPlayer serverPlayer, IQuestContext ctx)
        {
            if (serverPlayer == null) return;

            if (activeQuest.currentStageIndex >= 0 && activeQuest.currentStageIndex < quest.stages.Count)
            {
                var stage = quest.stages[activeQuest.currentStageIndex];
                if (stage?.interactObjectives != null)
                {
                    foreach (var interactObjective in stage.interactObjectives)
                    {
                        if (interactObjective.validCodes != null && interactObjective.validCodes.Any(x => string.Equals(x, blockCode, StringComparison.OrdinalIgnoreCase)))
                        {
                            _positionCache.UpdatePosition(activeQuest.questId, position);
                            // Handle interact reward logic here if needed
                        }
                    }
                }
            }

            // Check legacy objectives (no stages)
            if (quest.interactObjectives != null)
            {
                foreach (var interactObjective in quest.interactObjectives)
                {
                    if (interactObjective.validCodes != null && interactObjective.validCodes.Any(x => string.Equals(x, blockCode, StringComparison.OrdinalIgnoreCase)))
                    {
                        _positionCache.UpdatePosition(activeQuest.questId, position);
                        // Handle interact reward logic here if needed
                    }
                }
            }
        }
    }
}
