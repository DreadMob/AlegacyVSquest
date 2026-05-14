using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Handles admin commands for promo code management:
    /// create, delete, list, info, addreward, reload.
    /// </summary>
    public class PromoCodeAdminHandler
    {
        private readonly PromoCodeSystem promoSystem;
        private readonly ICoreServerAPI sapi;

        public PromoCodeAdminHandler(PromoCodeSystem promoSystem, ICoreServerAPI sapi)
        {
            this.promoSystem = promoSystem;
            this.sapi = sapi;
        }

        /// <summary>
        /// /avq promo create [code] [type] [maxUses]
        /// Creates a new empty promo code (rewards added separately via addreward).
        /// </summary>
        public TextCommandResult HandleCreate(TextCommandCallingArgs args)
        {
            string code = (string)args[0];
            string type = args[1] as string ?? "personal";
            int maxUses = args[2] is int mu ? mu : 0;

            // Validate type
            var validTypes = new[] { "single", "personal", "multi", "unlimited" };
            if (!validTypes.Contains(type.ToLowerInvariant()))
            {
                return TextCommandResult.Error($"Invalid type '{type}'. Valid: {string.Join(", ", validTypes)}");
            }

            var newCode = new PromoCode
            {
                code = code,
                type = type.ToLowerInvariant(),
                maxUses = maxUses,
                enabled = true
            };

            var (success, message) = promoSystem.CreateCode(newCode);
            return success ? TextCommandResult.Success(message) : TextCommandResult.Error(message);
        }

        /// <summary>
        /// /avq promo delete [code]
        /// </summary>
        public TextCommandResult HandleDelete(TextCommandCallingArgs args)
        {
            string code = (string)args[0];
            var (success, message) = promoSystem.DeleteCode(code);
            return success ? TextCommandResult.Success(message) : TextCommandResult.Error(message);
        }

        /// <summary>
        /// /avq promo list
        /// </summary>
        public TextCommandResult HandleList(TextCommandCallingArgs args)
        {
            var codes = promoSystem.GetAllCodes().ToList();
            if (codes.Count == 0)
            {
                return TextCommandResult.Success("No promo codes registered.");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Promo codes ({codes.Count}):");
            foreach (var code in codes)
            {
                int uses = promoSystem.GetUsageCount(code.code);
                string status = code.enabled ? "ON" : "OFF";
                string maxStr = code.type == "multi" ? $"/{code.maxUses}" : "";
                sb.AppendLine($"  [{status}] {code.code} ({code.type}) — {code.rewards.Count} rewards, {uses}{maxStr} uses");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// /avq promo info [code]
        /// </summary>
        public TextCommandResult HandleInfo(TextCommandCallingArgs args)
        {
            string code = (string)args[0];
            var promoCode = promoSystem.GetCode(code);

            if (promoCode == null)
            {
                return TextCommandResult.Error($"Code '{code}' not found.");
            }

            int uses = promoSystem.GetUsageCount(promoCode.code);
            var sb = new StringBuilder();
            sb.AppendLine($"Code: {promoCode.code}");
            sb.AppendLine($"Type: {promoCode.type}");
            sb.AppendLine($"Enabled: {promoCode.enabled}");
            sb.AppendLine($"Uses: {uses}" + (promoCode.maxUses > 0 ? $" / {promoCode.maxUses}" : ""));
            sb.AppendLine($"Rewards ({promoCode.rewards.Count}):");

            foreach (var reward in promoCode.rewards)
            {
                string itemRef = reward.itemId ?? reward.itemCode ?? reward.questId ?? reward.reputationId ?? "?";
                sb.AppendLine($"  - {reward.type}: {itemRef} x{reward.amount}");
            }

            if (promoCode.conditions != null)
            {
                if (!string.IsNullOrEmpty(promoCode.conditions.validFrom))
                    sb.AppendLine($"Valid from: {promoCode.conditions.validFrom}");
                if (!string.IsNullOrEmpty(promoCode.conditions.validUntil))
                    sb.AppendLine($"Valid until: {promoCode.conditions.validUntil}");
                if (promoCode.conditions.requiredQuests?.Count > 0)
                    sb.AppendLine($"Required quests: {string.Join(", ", promoCode.conditions.requiredQuests)}");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// /avq promo addreward [code] [rewardType] [itemId] [amount]
        /// Adds a reward to an existing promo code at runtime.
        /// rewardType: actionItem, item, quest, reputation
        /// </summary>
        public TextCommandResult HandleAddReward(TextCommandCallingArgs args)
        {
            string code = (string)args[0];
            string rewardType = (string)args[1];
            string itemId = (string)args[2];
            int amount = args[3] is int a ? a : 1;

            var validTypes = new[] { "actionitem", "item", "quest", "reputation" };
            if (!validTypes.Contains(rewardType.ToLowerInvariant()))
            {
                return TextCommandResult.Error($"Invalid reward type '{rewardType}'. Valid: {string.Join(", ", validTypes)}");
            }

            var reward = new PromoCodeReward
            {
                type = rewardType.ToLowerInvariant(),
                amount = amount
            };

            // Assign the ID to the appropriate field based on type
            switch (rewardType.ToLowerInvariant())
            {
                case "actionitem":
                    reward.itemId = itemId;
                    break;
                case "item":
                    reward.itemCode = itemId;
                    break;
                case "quest":
                    reward.questId = itemId;
                    break;
                case "reputation":
                    reward.reputationId = itemId;
                    reward.reputationAmount = amount;
                    break;
            }

            var (success, message) = promoSystem.AddReward(code, reward);
            return success ? TextCommandResult.Success(message) : TextCommandResult.Error(message);
        }

        /// <summary>
        /// /avq promo reload
        /// </summary>
        public TextCommandResult HandleReload(TextCommandCallingArgs args)
        {
            promoSystem.Reload();
            return TextCommandResult.Success("Promo code configs reloaded.");
        }

        /// <summary>
        /// /avq promo reset [playerName] [code]
        /// Resets a player's usage of a code so they can redeem it again.
        /// </summary>
        public TextCommandResult HandleReset(TextCommandCallingArgs args)
        {
            string playerName = args[0] as string;
            string code = args[1] as string;

            if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(code))
            {
                return TextCommandResult.Error("Usage: /avq promo reset <playerName> <code>");
            }

            // Find player UID by name — try online first, then all server players
            string playerUid = null;
            string resolvedName = playerName;

            foreach (var p in sapi.World.AllOnlinePlayers)
            {
                if (p != null && string.Equals(p.PlayerName, playerName, System.StringComparison.OrdinalIgnoreCase))
                {
                    playerUid = p.PlayerUID;
                    resolvedName = p.PlayerName;
                    break;
                }
            }

            if (playerUid == null)
            {
                try
                {
                    foreach (var p in sapi.Server.Players)
                    {
                        if (p != null && string.Equals(p.PlayerName, playerName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            playerUid = p.PlayerUID;
                            resolvedName = p.PlayerName;
                            break;
                        }
                    }
                }
                catch { /* Server.Players may not be available */ }
            }

            if (string.IsNullOrEmpty(playerUid))
            {
                return TextCommandResult.Error($"Player '{playerName}' not found. They must have logged in at least once.");
            }

            var (success, message) = promoSystem.ResetPlayerUsage(playerUid, resolvedName, code);
            return success ? TextCommandResult.Success(message) : TextCommandResult.Error(message);
        }
    }
}
