using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class RandomQuestItemAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args == null || args.Length < 1)
            {
                throw new QuestException("The 'randomquestitem' action requires at least 1 argument: actionItemId (or count + actionItemIds).");
            }

            int startIndex = 0;
            int count = 1;

            if (int.TryParse(args[0], out int parsedCount))
            {
                count = parsedCount;
                startIndex = 1;
            }

            if (count < 1)
            {
                throw new QuestException("The 'randomquestitem' action count must be >= 1.");
            }

            if (args.Length - startIndex < 1)
            {
                throw new QuestException("The 'randomquestitem' action requires at least 1 actionItemId.");
            }

            var pool = new List<string>();
            for (int i = startIndex; i < args.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(args[i])) pool.Add(args[i]);
            }

            if (pool.Count == 0)
            {
                throw new QuestException("The 'randomquestitem' action requires at least 1 non-empty actionItemId.");
            }

            if (count > pool.Count)
            {
                throw new QuestException($"The 'randomquestitem' action requires at least {count} distinct actionItemIds, but only {pool.Count} were provided.");
            }

            var itemSystem = sapi.ModLoader.GetModSystem<ItemSystem>();
            if (itemSystem == null)
            {
                throw new QuestException("ItemSystem not found for 'randomquestitem' action.");
            }

            for (int roll = 0; roll < count; roll++)
            {
                int idx = sapi.World.Rand.Next(0, pool.Count);
                string actionItemId = pool[idx];
                pool.RemoveAt(idx);

                if (!itemSystem.ActionItemRegistry.TryGetValue(actionItemId, out var actionItem))
                {
                    throw new QuestException($"Action item with ID '{actionItemId}' not found for 'randomquestitem' action in quest {message.questId}.");
                }

                if (!ItemAttributeUtils.TryResolveCollectible(sapi, actionItem.itemCode, out var collectible))
                {
                    throw new QuestException($"Base item/block with code '{actionItem.itemCode}' not found for action item '{actionItemId}' in quest {message.questId}.");
                }

                var stack = new ItemStack(collectible);
                int quantity = stack.StackSize;
                ItemAttributeUtils.ApplyActionItemAttributes(stack, actionItem);
                if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    sapi.World.SpawnItemEntity(stack, byPlayer.Entity.Pos.XYZ);
                }

                var itemName = collectible.GetHeldItemName(stack);
                sapi.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup, Lang.Get("alegacyvsquest:questitem-given", quantity, itemName), EnumChatType.Notification);
            }
        }
    }
}
