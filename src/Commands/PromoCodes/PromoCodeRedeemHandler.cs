using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Handles the player-facing /promo command that opens the promo code GUI.
    /// </summary>
    public class PromoCodeRedeemHandler
    {
        private readonly PromoCodeSystem promoSystem;
        private readonly PromoCodeNetworkHandler networkHandler;
        private readonly ICoreServerAPI sapi;

        public PromoCodeRedeemHandler(PromoCodeSystem promoSystem, PromoCodeNetworkHandler networkHandler, ICoreServerAPI sapi)
        {
            this.promoSystem = promoSystem;
            this.networkHandler = networkHandler;
            this.sapi = sapi;
        }

        /// <summary>
        /// /promo — Opens the promo code GUI for the player.
        /// </summary>
        public TextCommandResult HandleOpenGui(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            if (player == null) return TextCommandResult.Error("This command can only be run by a player.");

            PromoCodeNetworkHandler.SendOpenGui(sapi, player);
            return TextCommandResult.Success();
        }
    }
}
