using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Command handler for setting item quality.
    /// Usage: /vsquest setquality [player] [qualityId]
    ///        /vsquest rerollquality [player]
    /// </summary>
    public class SetItemQualityCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public SetItemQualityCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public TextCommandResult HandleSetQuality(TextCommandCallingArgs args)
        {
            if (args.Parsers.Count < 2)
            {
                return TextCommandResult.Error("Usage: /vsquest setquality [player] [qualityId]");
            }

            string playerName = args.Parsers[0].GetValue() as string;
            string qualityId = args.Parsers[1].GetValue() as string;

            // Find player
            IServerPlayer targetPlayer = null;
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                targetPlayer = sapi.Server.Players.FirstOrDefault(p => p?.PlayerName?.ToLowerInvariant() == playerName.ToLowerInvariant());
            }
            else
            {
                // Use caller
                targetPlayer = args.Caller.Player as IServerPlayer;
            }

            if (targetPlayer == null)
            {
                return TextCommandResult.Error("Player not found.");
            }

            // Get active hotbar slot (item in hand)
            var activeSlot = targetPlayer.InventoryManager.ActiveHotbarSlot;
            
            if (activeSlot == null || activeSlot.Empty)
            {
                return TextCommandResult.Error("No item found in player's active hotbar slot (item in hand).");
            }

            var stack = activeSlot.Itemstack;
            if (stack.Attributes == null)
            {
                return TextCommandResult.Error("This item does not support quality attributes.");
            }

            // Get quality service
            var itemSystem = sapi.ModLoader.GetModSystem<ItemSystem>();
            var qualityService = itemSystem?.QualityService;
            if (qualityService == null)
            {
                return TextCommandResult.Error("Quality service not available.");
            }

            // Find quality config
            var quality = qualityService.GetQuality(qualityId);
            if (quality == null)
            {
                return TextCommandResult.Error($"Quality '{qualityId}' not found. Available qualities: {string.Join(", ", qualityService.GetAllQualities().Select(q => q.id))}");
            }

            // Get action item for the stack to apply quality properly
            string actionItemId = stack.Attributes.GetString(ItemAttributeUtils.ActionItemIdKey);
            if (string.IsNullOrWhiteSpace(actionItemId))
            {
                return TextCommandResult.Error("This item is not an action item (no actionItemId attribute).");
            }

            // Get ActionItem from registry
            if (!itemSystem.ActionItemRegistry.TryGetValue(actionItemId, out var actionItem))
            {
                return TextCommandResult.Error($"Action item '{actionItemId}' not found in registry.");
            }

            // Apply quality with full attribute calculation
            qualityService.ApplyQuality(stack, actionItem, quality, sapi.World.Rand);

            activeSlot.MarkDirty();

            return TextCommandResult.Success($"Set quality '{quality.name}' on item for player {targetPlayer.PlayerName}.");
        }

        public TextCommandResult HandleRerollQuality(TextCommandCallingArgs args)
        {
            string playerName = args.Parsers.Count > 0 ? args.Parsers[0].GetValue() as string : null;

            // Find player
            IServerPlayer targetPlayer = null;
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                targetPlayer = sapi.Server.Players.FirstOrDefault(p => p?.PlayerName?.ToLowerInvariant() == playerName.ToLowerInvariant());
            }
            else
            {
                // Use caller
                targetPlayer = args.Caller.Player as IServerPlayer;
            }

            if (targetPlayer == null)
            {
                return TextCommandResult.Error("Player not found.");
            }

            // Get active hotbar slot (item in hand)
            var activeSlot = targetPlayer.InventoryManager.ActiveHotbarSlot;
            
            if (activeSlot == null || activeSlot.Empty)
            {
                return TextCommandResult.Error("No item found in player's active hotbar slot (item in hand).");
            }

            var stack = activeSlot.Itemstack;
            if (stack.Attributes == null)
            {
                return TextCommandResult.Error("This item does not support quality attributes.");
            }

            // Get quality service
            var itemSystem = sapi.ModLoader.GetModSystem<ItemSystem>();
            var qualityService = itemSystem?.QualityService;
            if (qualityService == null)
            {
                return TextCommandResult.Error("Quality service not available.");
            }

            // Get action item id from stack
            string actionItemId = stack.Attributes.GetString(ItemAttributeUtils.ActionItemIdKey);
            if (string.IsNullOrWhiteSpace(actionItemId))
            {
                return TextCommandResult.Error("This item is not an action item (no actionItemId attribute).");
            }

            // Get ActionItem from registry
            if (!itemSystem.ActionItemRegistry.TryGetValue(actionItemId, out var actionItem))
            {
                return TextCommandResult.Error($"Action item '{actionItemId}' not found in registry.");
            }

            // Roll random quality
            var qualities = qualityService.GetAllQualities().ToList();
            if (qualities.Count == 0)
            {
                return TextCommandResult.Error("No qualities configured.");
            }

            // Weighted random selection based on chance
            float totalWeight = qualities.Sum(q => q.chance > 0 ? q.chance : 0.1f);
            float roll = (float)sapi.World.Rand.NextDouble() * totalWeight;
            float cumulative = 0;
            var selectedQuality = qualities[0];

            foreach (var q in qualities)
            {
                float weight = q.chance > 0 ? q.chance : 0.1f;
                cumulative += weight;
                if (roll <= cumulative)
                {
                    selectedQuality = q;
                    break;
                }
            }

            // Apply quality with full attribute calculation
            qualityService.ApplyQuality(stack, actionItem, selectedQuality, sapi.World.Rand);

            activeSlot.MarkDirty();

            return TextCommandResult.Success($"Rolled quality '{selectedQuality.name}' on item for player {targetPlayer.PlayerName}.");
        }
    }
}
