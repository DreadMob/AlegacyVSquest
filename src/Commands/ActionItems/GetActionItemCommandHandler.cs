using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class GetActionItemCommandHandler
    {
        private readonly ICoreAPI api;
        private readonly ItemSystem itemSystem;

        public GetActionItemCommandHandler(ICoreAPI api, ItemSystem itemSystem)
        {
            this.api = api;
            this.itemSystem = itemSystem;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            if (player == null) return TextCommandResult.Error("This command can only be run by a player.");

            string itemId = (string)args[0];
            int amount = (int)args[1];

            if (!itemSystem.ActionItemRegistry.TryGetValue(itemId, out var actionItem))
            {
                return TextCommandResult.Error($"Action item with ID '{itemId}' not found in itemconfig.json.");
            }

            if (!ItemAttributeUtils.TryResolveCollectible(api, actionItem.itemCode, out var collectible))
            {
                return TextCommandResult.Error($"Could not find base item/block with code '{actionItem.itemCode}'.");
            }

            var stack = new ItemStack(collectible, amount);
            ItemAttributeUtils.ApplyActionItemAttributes(stack, actionItem);

            // Apply quality roll (may override the action item)
            var qualityService = itemSystem?.QualityService;
            if (qualityService != null)
            {
                string overrideId = null;
                qualityService.TryApplyQuality(stack, actionItem, api.World.Rand, out overrideId);

                // If quality overrides the action item, recreate the stack with the new item
                if (!string.IsNullOrWhiteSpace(overrideId) && overrideId != actionItem.id)
                {
                    if (itemSystem.ActionItemRegistry.TryGetValue(overrideId, out var overrideActionItem))
                    {
                        if (ItemAttributeUtils.TryResolveCollectible(api, overrideActionItem.itemCode, out var overrideCollectible))
                        {
                            // Save quality info from the original roll
                            string qId = stack.Attributes.GetString(ItemAttributeUtils.ItemQualityIdKey);
                            string qName = stack.Attributes.GetString(ItemAttributeUtils.ItemQualityNameKey);
                            string qColor = stack.Attributes.GetString(ItemAttributeUtils.ItemQualityColorKey);

                            // Create new stack with override item
                            stack = new ItemStack(overrideCollectible, amount);
                            ItemAttributeUtils.ApplyActionItemAttributes(stack, overrideActionItem);

                            // Re-apply quality header info (no bonus scaling needed - attributes are already correct)
                            if (!string.IsNullOrEmpty(qId))
                            {
                                stack.Attributes.SetString(ItemAttributeUtils.ItemQualityIdKey, qId);
                                stack.Attributes.SetString(ItemAttributeUtils.ItemQualityNameKey, qName);
                                stack.Attributes.SetString(ItemAttributeUtils.ItemQualityColorKey, qColor);
                                stack.Attributes.SetFloat(ItemAttributeUtils.ItemQualityBonusPercentKey, 0f);
                            }

                            actionItem = overrideActionItem;
                        }
                    }
                }
            }

            if (!player.InventoryManager.TryGiveItemstack(stack))
            {
                api.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
            }

            return TextCommandResult.Success($"Successfully gave {amount}x {actionItem.name}.");
        }
    }
}
