using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Handles giving rewards to players when a promo code is redeemed.
    /// Supports: actionItem, item, quest, reputation.
    /// </summary>
    public class PromoCodeRewardGiver
    {
        private readonly ICoreServerAPI sapi;
        private readonly ItemSystem itemSystem;
        private readonly QuestSystem questSystem;

        public PromoCodeRewardGiver(ICoreServerAPI sapi, ItemSystem itemSystem, QuestSystem questSystem)
        {
            this.sapi = sapi;
            this.itemSystem = itemSystem;
            this.questSystem = questSystem;
        }

        /// <summary>
        /// Give all rewards from a promo code to a player.
        /// Returns a list of reward descriptions for feedback.
        /// </summary>
        public List<string> GiveRewards(PromoCode promoCode, IServerPlayer player)
        {
            var results = new List<string>();

            foreach (var reward in promoCode.rewards)
            {
                try
                {
                    string result = GiveReward(reward, player);
                    if (!string.IsNullOrEmpty(result))
                    {
                        results.Add(result);
                    }
                }
                catch (Exception ex)
                {
                    sapi.Logger.Error("[PromoCode] Failed to give reward ({0}) to player {1}: {2}", reward.type, player.PlayerName, ex.Message);
                    results.Add($"[Error: {reward.type}]");
                }
            }

            return results;
        }

        private string GiveReward(PromoCodeReward reward, IServerPlayer player)
        {
            switch (reward.type?.ToLowerInvariant())
            {
                case "actionitem":
                    return GiveActionItem(reward, player);
                case "item":
                    return GiveVanillaItem(reward, player);
                case "quest":
                    return GiveQuest(reward, player);
                case "reputation":
                    return GiveReputation(reward, player);
                default:
                    sapi.Logger.Warning("[PromoCode] Unknown reward type: {0}", reward.type);
                    return null;
            }
        }

        private string GiveActionItem(PromoCodeReward reward, IServerPlayer player)
        {
            string itemId = reward.itemId;
            if (string.IsNullOrWhiteSpace(itemId)) return null;

            if (!itemSystem.ActionItemRegistry.TryGetValue(itemId, out var actionItem))
            {
                sapi.Logger.Warning("[PromoCode] Action item '{0}' not found in registry", itemId);
                return null;
            }

            if (!ItemAttributeUtils.TryResolveCollectible(sapi, actionItem.itemCode, out var collectible))
            {
                sapi.Logger.Warning("[PromoCode] Could not resolve collectible for action item '{0}' (code: {1})", itemId, actionItem.itemCode);
                return null;
            }

            int amount = Math.Max(1, reward.amount);
            var stack = new ItemStack(collectible, amount);
            ItemAttributeUtils.ApplyActionItemAttributes(stack, actionItem);

            // Apply quality if requested
            if (reward.applyQuality)
            {
                var qualityService = itemSystem.QualityService;
                if (qualityService != null)
                {
                    qualityService.TryApplyQuality(stack, actionItem, sapi.World.Rand, out _);
                }
            }

            GiveItemToPlayer(stack, player);
            return $"{amount}x {actionItem.name ?? itemId}";
        }

        private string GiveVanillaItem(PromoCodeReward reward, IServerPlayer player)
        {
            string code = reward.itemCode ?? reward.itemId;
            if (string.IsNullOrWhiteSpace(code)) return null;

            if (!ItemAttributeUtils.TryResolveCollectible(sapi, code, out var collectible))
            {
                sapi.Logger.Warning("[PromoCode] Could not resolve item/block '{0}'", code);
                return null;
            }

            int amount = Math.Max(1, reward.amount);
            var stack = new ItemStack(collectible, amount);

            GiveItemToPlayer(stack, player);

            string name = collectible.GetHeldItemName(stack);
            return $"{amount}x {name}";
        }

        private string GiveQuest(PromoCodeReward reward, IServerPlayer player)
        {
            string questId = reward.questId ?? reward.itemId;
            if (string.IsNullOrWhiteSpace(questId)) return null;

            // Quest starting is complex — for now just log it. 
            // Full implementation would call questSystem lifecycle methods.
            sapi.Logger.Notification("[PromoCode] Quest reward '{0}' for player {1} — quest start not yet implemented via promo", questId, player.PlayerName);
            return $"Quest: {questId}";
        }

        private string GiveReputation(PromoCodeReward reward, IServerPlayer player)
        {
            if (string.IsNullOrWhiteSpace(reward.reputationId) || reward.reputationAmount == 0) return null;

            try
            {
                var reputationSystem = sapi.ModLoader.GetModSystem<ReputationSystem>();
                if (reputationSystem == null)
                {
                    sapi.Logger.Warning("[PromoCode] ReputationSystem not available");
                    return null;
                }

                var scope = reward.reputationType == "faction" ? ReputationScope.Faction : ReputationScope.Npc;
                int currentValue = reputationSystem.GetReputationValue(player, scope, reward.reputationId);
                int newValue = currentValue + reward.reputationAmount;
                reputationSystem.ApplyReputationChange(sapi, player, scope, reward.reputationId, newValue, true);

                return $"Reputation +{reward.reputationAmount} ({reward.reputationId})";
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[PromoCode] Failed to give reputation: {0}", ex.Message);
                return null;
            }
        }

        private void GiveItemToPlayer(ItemStack stack, IServerPlayer player)
        {
            if (!player.InventoryManager.TryGiveItemstack(stack))
            {
                sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
            }
        }
    }
}
