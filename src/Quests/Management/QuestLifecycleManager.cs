using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using System.Linq;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestLifecycleManager : IQuestLifecycleManager
    {
        private readonly IQuestRegistryService questRegistryService;
        private readonly Dictionary<string, Quest> questRegistry;
        private readonly Dictionary<string, IQuestAction> actionRegistry;
        private readonly ICoreAPI api;
        private readonly IQuestNotificationService notificationService;
        private readonly IQuestRewardService rewardService;
        private readonly IQuestCompletionService completionService;
        private readonly IQuestStateManager stateManager;

        public QuestLifecycleManager(ICoreAPI api, IQuestStateManager stateManager = null, IQuestRegistryService questRegistryService = null)
        {
            this.questRegistryService = questRegistryService ?? QuestRegistryService.Instance;
            this.questRegistry = this.questRegistryService.QuestRegistry;
            this.actionRegistry = this.questRegistryService.ActionRegistry;
            this.api = api;
            this.notificationService = new QuestNotificationService(api);
            this.rewardService = new QuestRewardService();
            this.completionService = new QuestCompletionService(notificationService, rewardService);
            this.stateManager = stateManager ?? new QuestStateManager();
        }

        public void OnQuestAccepted(IServerPlayer fromPlayer, QuestAcceptedMessage message, ICoreServerAPI sapi, System.Func<string, List<ActiveQuest>> getPlayerQuests)
        {
            if (!questRegistry.TryGetValue(message.questId, out var quest))
            {
                sapi.Logger.Error($"[alegacyvsquest] Could not accept quest with id '{message.questId}' because it was not found in the QuestRegistry.");
                return;
            }

            Systems.Interaction.InteractionService.ResetCompletedInteractAtObjectives(quest, fromPlayer);
            QuestObjectiveCleanupUtil.ClearKillActionTargetProgressOnAccept(fromPlayer, quest);
            var playerQuests = getPlayerQuests(fromPlayer.PlayerUID);

            if (playerQuests.Exists(q => q.questId == message.questId))
            {
                return;
            }

            QuestActionObjectiveCompletionUtil.ResetCompletionFlags(quest, fromPlayer);

            // Create new ActiveQuest with simplified structure
            var activeQuest = new ActiveQuest
            {
                questGiverId = message.questGiverId,
                questId = message.questId
            };
            
            activeQuest.SetStateManager(stateManager);
            playerQuests.Add(activeQuest);
            foreach (var action in quest.onAcceptedActions)
            {
                try
                {
                    actionRegistry[action.id].Execute(sapi, message, fromPlayer, action.args);
                }
                catch (Exception ex)
                {
                    sapi.Logger.Error($"Action {action.id} caused an Error in Quest {quest.id}. The Error had the following message: {ex.Message}\n Stacktrace: {ex.StackTrace}");
                }
            }

            // Send updated quest info to client immediately after accepting
            var questgiver = sapi.World.GetEntityById(message.questGiverId);
            if (questgiver != null)
            {
                var questGiverBehavior = questgiver.GetBehavior<EntityBehaviorQuestGiver>();
                questGiverBehavior?.SendQuestInfoMessageToClient(sapi, fromPlayer.Entity);
            }

            try
            {
                QuestObjectiveAnnounceUtil.AnnounceOnAccept(fromPlayer, message, sapi, quest);
            }
            catch (Exception e)
            {
                sapi.Logger.Warning($"[alegacyvsquest] Error announcing quest objective on accept for quest '{message.questId}': {e.Message}");
            }
        }

        public void OnQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi, System.Func<string, List<ActiveQuest>> getPlayerQuests)
        {
            TryCompleteQuest(fromPlayer, message, sapi, getPlayerQuests, force: false);
        }

        public bool ForceCompleteQuest(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi, System.Func<string, List<ActiveQuest>> getPlayerQuests)
        {
            return TryCompleteQuest(fromPlayer, message, sapi, getPlayerQuests, force: true);
        }

        private bool TryCompleteQuest(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi, System.Func<string, List<ActiveQuest>> getPlayerQuests, bool force)
        {
            var playerQuests = getPlayerQuests(fromPlayer.PlayerUID);
            var activeQuest = playerQuests.Find(item => item.questId == message.questId);

            if (activeQuest == null)
            {
                if (!force)
                {
                    sapi.Logger.Warning($"[alegacyvsquest] Player {fromPlayer.PlayerName} attempted to complete quest '{message.questId}' which is not active.");
                }
                return false;
            }

            if (!questRegistry.TryGetValue(message.questId, out var quest))
            {
                if (!force)
                {
                    sapi.Logger.Error($"[alegacyvsquest] Could not complete quest with id '{message.questId}' because it was not found in the QuestRegistry.");
                }
                return false;
            }

            // Check if this is a multi-stage quest and we're not on the final stage
            if (quest.HasStages && activeQuest.currentStageIndex < quest.StageCount - 1)
            {
                // For force complete, skip stage validation
                if (!force && !activeQuest.IsCurrentStageCompletable(fromPlayer, quest))
                {
                    sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup,
                        LocalizationUtils.GetSafe("alegacyvsquest:quest-could-not-complete"), EnumChatType.Notification);
                    return false;
                }

                // Execute stage completion actions
                var currentStage = quest.GetStage(activeQuest.currentStageIndex);
                if (currentStage?.onStageCompleteActions != null)
                {
                    foreach (var action in currentStage.onStageCompleteActions)
                    {
                        try
                        {
                            if (actionRegistry.TryGetValue(action.id, out var actionImpl))
                            {
                                actionImpl.Execute(sapi, message, fromPlayer, action.args);
                            }
                        }
                        catch (Exception ex)
                        {
                            sapi.Logger.Error($"[alegacyvsquest] Stage completion action {action.id} caused an error: {ex.Message}");
                        }
                    }
                }

                // Play stage complete sound if configured
                if (!string.IsNullOrEmpty(currentStage?.stageCompleteSound))
                {
                    sapi.World.PlaySoundFor(new AssetLocation(currentStage.stageCompleteSound), fromPlayer, 1f, 32f, 1f);
                }

                // Advance to next stage
                activeQuest.AdvanceStage(quest);

                // Send updated quest info
                var questgiver = sapi.World.GetEntityById(message.questGiverId);
                if (questgiver != null)
                {
                    var questGiverBehavior = questgiver.GetBehavior<EntityBehaviorQuestGiver>();
                    questGiverBehavior?.SendQuestInfoMessageToClient(sapi, fromPlayer.Entity);
                }

                // For non-final stages, we're done (quest not fully completed yet)
                return true;
            }

            // Final stage or single-stage quest
            if (!force && !activeQuest.IsCompletable(fromPlayer))
            {
                sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup,
                    LocalizationUtils.GetSafe("alegacyvsquest:quest-could-not-complete"), EnumChatType.Notification);
                return false;
            }

            var finalQuestgiver = sapi.World.GetEntityById(message.questGiverId);
            completionService.CompleteQuest(fromPlayer, message, sapi, finalQuestgiver, playerQuests, activeQuest);
            return true;
        }
    }
}
