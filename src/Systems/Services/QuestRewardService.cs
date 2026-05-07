using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestRewardService : IQuestRewardService
    {
        private readonly IQuestRegistryService registryService;

        public QuestRewardService(IQuestRegistryService registryService = null)
        {
            this.registryService = registryService ?? QuestRegistryService.Instance;
        }

        private Dictionary<string, Quest> questRegistry => registryService.QuestRegistry;
        private Dictionary<string, IQuestAction> actionRegistry => registryService.ActionRegistry;

        public void RewardPlayer(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi, Entity questgiver)
        {
            if (!questRegistry.TryGetValue(message.questId, out var quest))
            {
                sapi.Logger.Error($"[alegacyvsquest] Could not reward player for quest with id '{message.questId}' because it was not found in the QuestRegistry.");
                return;
            }

            GiveItemRewards(fromPlayer, quest, sapi, questgiver);
            GiveRandomItemRewards(fromPlayer, quest, sapi, questgiver);
            ExecuteActionRewards(fromPlayer, message, quest, sapi);
        }

        private void GiveItemRewards(IServerPlayer fromPlayer, Quest quest, ICoreServerAPI sapi, Entity questgiver)
        {
            foreach (var reward in quest.itemRewards)
            {
                CollectibleObject item = sapi.World.GetItem(new AssetLocation(reward.itemCode));
                if (item == null)
                {
                    item = sapi.World.GetBlock(new AssetLocation(reward.itemCode));
                }
                if (item == null)
                {
                    sapi.Logger.Error($"alegacyvsquest: Quest '{quest.id}' has invalid item reward code '{reward.itemCode}'. Skipping reward.");
                    continue;
                }

                var stack = new ItemStack(item, reward.amount);
                if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    sapi.World.SpawnItemEntity(stack, (questgiver ?? fromPlayer.Entity).Pos.XYZ);
                }
            }
        }

        private void GiveRandomItemRewards(IServerPlayer fromPlayer, Quest quest, ICoreServerAPI sapi, Entity questgiver)
        {
            var randomItems = quest.randomItemRewards?.items == null
                ? new List<RandomItem>()
                : new List<RandomItem>(quest.randomItemRewards.items);

            int selectAmount = quest.randomItemRewards?.selectAmount ?? 0;
            for (int i = 0; i < selectAmount; i++)
            {
                if (randomItems.Count <= 0) break;
                var randomItem = randomItems[sapi.World.Rand.Next(0, randomItems.Count)];
                randomItems.Remove(randomItem);
                CollectibleObject item = sapi.World.GetItem(new AssetLocation(randomItem.itemCode));
                if (item == null)
                {
                    item = sapi.World.GetBlock(new AssetLocation(randomItem.itemCode));
                }
                if (item == null)
                {
                    sapi.Logger.Error($"alegacyvsquest: Quest '{quest.id}' has invalid random item reward code '{randomItem.itemCode}'. Skipping reward.");
                    continue;
                }

                var stack = new ItemStack(item, sapi.World.Rand.Next(randomItem.minAmount, randomItem.maxAmount + 1));
                if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    sapi.World.SpawnItemEntity(stack, (questgiver ?? fromPlayer.Entity).Pos.XYZ);
                }
            }
        }

        private void ExecuteActionRewards(IServerPlayer fromPlayer, QuestCompletedMessage message, Quest quest, ICoreServerAPI sapi)
        {
            foreach (var action in quest.actionRewards)
            {
                try
                {
                    actionRegistry[action.id].Execute(sapi, message, fromPlayer, action.args);
                }
                catch (Exception ex)
                {
                    sapi.Logger.Error($"Action {action.id} caused an Error in Quest {quest.id}. The Error had the following message: {ex.Message}\n Stacktrace: {ex.StackTrace}");
                    sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup, Lang.Get("alegacyvsquest:quest-action-error", quest.id), EnumChatType.Notification);
                }
            }
        }
    }
}
