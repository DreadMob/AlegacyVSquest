using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Registers promo code admin commands under /avq promo.
    /// Also registers the player-facing /promo command.
    /// </summary>
    public class PromoCodeChatCommandSubRegistry : IChatCommandSubRegistry
    {
        private readonly PromoCodeSystem promoSystem;
        private readonly PromoCodeNetworkHandler networkHandler;

        public PromoCodeChatCommandSubRegistry(PromoCodeSystem promoSystem, PromoCodeNetworkHandler networkHandler)
        {
            this.promoSystem = promoSystem;
            this.networkHandler = networkHandler;
        }

        public void Register(IChatCommand avq, ICoreServerAPI sapi)
        {
            var adminHandler = new PromoCodeAdminHandler(promoSystem, sapi);
            var redeemHandler = new PromoCodeRedeemHandler(promoSystem, networkHandler, sapi);

            // Admin commands: /avq promo ...
            avq.BeginSubCommand("promo")
                .WithDescription("Promo code administration.")
                .RequiresPrivilege(Privilege.give)

                .BeginSubCommand("create")
                    .WithDescription("Create a new promo code. Usage: /avq promo create <code> <type> [maxUses]")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(
                        sapi.ChatCommands.Parsers.Word("code"),
                        sapi.ChatCommands.Parsers.OptionalWord("type"),
                        sapi.ChatCommands.Parsers.OptionalInt("maxUses", 0)
                    )
                    .HandleWith(adminHandler.HandleCreate)
                .EndSubCommand()

                .BeginSubCommand("delete")
                    .WithDescription("Delete a promo code.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.Word("code"))
                    .HandleWith(adminHandler.HandleDelete)
                .EndSubCommand()

                .BeginSubCommand("list")
                    .WithDescription("List all promo codes.")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(adminHandler.HandleList)
                .EndSubCommand()

                .BeginSubCommand("info")
                    .WithDescription("Show info about a promo code.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.Word("code"))
                    .HandleWith(adminHandler.HandleInfo)
                .EndSubCommand()

                .BeginSubCommand("addreward")
                    .WithDescription("Add a reward to an existing code. Usage: /avq promo addreward <code> <rewardType> <itemId> [amount]")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(
                        sapi.ChatCommands.Parsers.Word("code"),
                        sapi.ChatCommands.Parsers.Word("rewardType"),
                        sapi.ChatCommands.Parsers.Word("itemId"),
                        sapi.ChatCommands.Parsers.OptionalInt("amount", 1)
                    )
                    .HandleWith(adminHandler.HandleAddReward)
                .EndSubCommand()

                .BeginSubCommand("reload")
                    .WithDescription("Reload promo code configs.")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(adminHandler.HandleReload)
                .EndSubCommand()

                .BeginSubCommand("reset")
                    .WithDescription("Reset a player's usage of a code. Usage: /avq promo reset <playerName> <code>")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(
                        sapi.ChatCommands.Parsers.Word("playerName"),
                        sapi.ChatCommands.Parsers.Word("code")
                    )
                    .HandleWith(adminHandler.HandleReset)
                .EndSubCommand()

            .EndSubCommand();

            // Player-facing command: /promo
            sapi.ChatCommands.Create("promo")
                .WithDescription("Open the promo code redemption dialog.")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith(redeemHandler.HandleOpenGui);
        }
    }
}
