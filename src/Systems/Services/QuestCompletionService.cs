using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestCompletionService : IQuestCompletionService
    {
        private readonly IQuestNotificationService notificationService;
        private readonly IQuestRewardService rewardService;
        private readonly Dictionary<string, Quest> questRegistry = QuestRegistryService.QuestRegistry;

        public QuestCompletionService(
            IQuestNotificationService notificationService,
            IQuestRewardService rewardService)
        {
            this.notificationService = notificationService;
            this.rewardService = rewardService;
        }

        public void CompleteQuest(
            IServerPlayer fromPlayer,
            QuestCompletedMessage message,
            ICoreServerAPI sapi,
            Entity questgiver,
            List<ActiveQuest> playerQuests,
            ActiveQuest activeQuest)
        {
            if (activeQuest == null) return;

            // Complete the quest
            activeQuest.CompleteQuest(fromPlayer);
            playerQuests.Remove(activeQuest);

            // Get quest for notification and timestamp
            questRegistry.TryGetValue(message.questId, out var quest);

            // Reward player
            rewardService.RewardPlayer(fromPlayer, message, sapi, questgiver);

            // Mark as completed
            MarkQuestCompleted(fromPlayer, message, questgiver, questRegistry);

            // Broadcast notification
            if (notificationService.ShouldNotifyOnComplete(quest))
            {
                notificationService.BroadcastQuestCompleted(fromPlayer, message.questId);
            }

            // Update cooldown and timestamps
            UpdateCompletionTimestamps(fromPlayer, message, sapi, questgiver, quest);
        }

        private void MarkQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, Entity questgiver, Dictionary<string, Quest> registry)
        {
            var key = "alegacyvsquest:playercompleted";
            var completedQuests = fromPlayer.Entity.WatchedAttributes.GetStringArray(key, new string[0]).ToList();
            string questId = NormalizeQuestId(message.questId, completedQuests, registry);
            if (!completedQuests.Contains(questId))
            {
                completedQuests.Add(questId);
                fromPlayer.Entity.WatchedAttributes.SetStringArray(key, completedQuests.ToArray());
                fromPlayer.Entity.WatchedAttributes.MarkPathDirty(key);
            }
        }

        private static string NormalizeQuestId(string questId, List<string> completedQuests, Dictionary<string, Quest> registry)
        {
            if (string.IsNullOrWhiteSpace(questId)) return questId;
            if (registry != null && registry.ContainsKey(questId)) return questId;

            const string legacyPrefix = "vsquest:";
            const string currentPrefix = "alegacyvsquest:";

            if (questId.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string mapped = currentPrefix + questId.Substring(legacyPrefix.Length);
                if ((registry != null && registry.ContainsKey(mapped))
                    || (completedQuests != null && completedQuests.Contains(mapped)))
                {
                    return mapped;
                }
            }

            return questId;
        }

        private void UpdateCompletionTimestamps(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi, Entity questgiver, Quest quest)
        {
            // Questgiver chain cooldown timestamp
            if (fromPlayer?.Entity?.WatchedAttributes != null)
            {
                string chainKey = EntityBehaviorQuestGiver.ChainCooldownLastCompletedKey(message.questGiverId);
                fromPlayer.Entity.WatchedAttributes.SetDouble(chainKey, sapi.World.Calendar.TotalDays);
                fromPlayer.Entity.WatchedAttributes.MarkPathDirty(chainKey);
            }

            // Update last accepted timestamp
            if (quest != null)
            {
                var key = $"alegacyvsquest:lastaccepted-{quest.id}";
                fromPlayer.Entity.WatchedAttributes.SetDouble(key, sapi.World.Calendar.TotalDays);
                fromPlayer.Entity.WatchedAttributes.MarkPathDirty(key);
            }
        }
    }
}
