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

            // Find item in player's hotbar (active slot)
            var hotbar = targetPlayer.InventoryManager.GetHotbarInventory();
            ItemSlot activeSlot = null;
            for (int i = 0; i < hotbar.Count; i++)
            {
                if (!hotbar[i].Empty)
                {
                    activeSlot = hotbar[i];
                    break;
                }
            }

            if (activeSlot == null || activeSlot.Empty)
            {
                return TextCommandResult.Error("No item found in player's hotbar.");
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

            // Apply quality
            stack.Attributes.SetString(ItemAttributeUtils.ItemQualityIdKey, quality.id);
            stack.Attributes.SetString(ItemAttributeUtils.ItemQualityNameKey, quality.name);
            stack.Attributes.SetString(ItemAttributeUtils.ItemQualityColorKey, quality.color);

            // Roll bonus percent
            float bonusPercent = (float)sapi.World.Rand.NextDouble() * (quality.maxBonusPercent - quality.minBonusPercent) + quality.minBonusPercent;
            stack.Attributes.SetFloat(ItemAttributeUtils.ItemQualityBonusPercentKey, bonusPercent);

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

            // Find item in player's hotbar (active slot)
            var hotbar = targetPlayer.InventoryManager.GetHotbarInventory();
            ItemSlot activeSlot = null;
            for (int i = 0; i < hotbar.Count; i++)
            {
                if (!hotbar[i].Empty)
                {
                    activeSlot = hotbar[i];
                    break;
                }
            }

            if (activeSlot == null || activeSlot.Empty)
            {
                return TextCommandResult.Error("No item found in player's hotbar.");
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

            // Roll random quality
            var qualities = qualityService.GetAllQualities().ToList();
            if (qualities.Count == 0)
            {
                return TextCommandResult.Error("No qualities configured.");
            }

            // Weighted random selection based on chance (inverse - lower chance = rarer = higher weight)
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

            // Apply quality
            stack.Attributes.SetString(ItemAttributeUtils.ItemQualityIdKey, selectedQuality.id);
            stack.Attributes.SetString(ItemAttributeUtils.ItemQualityNameKey, selectedQuality.name);
            stack.Attributes.SetString(ItemAttributeUtils.ItemQualityColorKey, selectedQuality.color);

            // Roll bonus percent
            float bonusPercent = (float)sapi.World.Rand.NextDouble() * (selectedQuality.maxBonusPercent - selectedQuality.minBonusPercent) + selectedQuality.minBonusPercent;
            stack.Attributes.SetFloat(ItemAttributeUtils.ItemQualityBonusPercentKey, bonusPercent);

            activeSlot.MarkDirty();

            return TextCommandResult.Success($"Rolled quality '{selectedQuality.name}' on item for player {targetPlayer.PlayerName}.");
        }
    }
}
