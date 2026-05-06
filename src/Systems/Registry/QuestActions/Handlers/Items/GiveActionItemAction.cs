using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class GiveActionItemAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 1)
            {
                throw new QuestException("The 'questitem' action requires at least 1 argument: actionItemId.");
            }

            var itemSystem = sapi.ModLoader.GetModSystem<ItemSystem>();
            if (!itemSystem.ActionItemRegistry.TryGetValue(args[0], out var actionItem))
            {
                throw new QuestException($"Action item with ID '{args[0]}' not found for 'questitem' action in quest {message.questId}.");
            }

            if (!ItemAttributeUtils.TryResolveCollectible(sapi, actionItem.itemCode, out var collectible))
            {
                throw new QuestException($"Base item/block with code '{actionItem.itemCode}' not found for action item '{args[0]}' in quest {message.questId}.");
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
