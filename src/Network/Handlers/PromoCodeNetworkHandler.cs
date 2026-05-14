using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Handles promo code network channel registration and message handling.
    /// </summary>
    public class PromoCodeNetworkHandler
    {
        public const string ChannelName = "alegacyvsquest-promo";

        private PromoCodeSystem promoSystem;
        private PromoCodeGui activeGui;
        private ICoreClientAPI capi;

        #region Server Side

        public void RegisterServer(ICoreServerAPI sapi, PromoCodeSystem promoSystem)
        {
            this.promoSystem = promoSystem;

            sapi.Network.RegisterChannel(ChannelName)
                .RegisterMessageType<RedeemPromoCodeMessage>()
                .SetMessageHandler<RedeemPromoCodeMessage>(OnRedeemMessage)
                .RegisterMessageType<PromoCodeResultMessage>()
                .RegisterMessageType<ShowPromoCodeGuiMessage>();
        }

        private void OnRedeemMessage(IServerPlayer player, RedeemPromoCodeMessage message)
        {
            if (promoSystem == null) return;

            var (success, msgKey) = promoSystem.Redeem(message.Code, player);

            // Resolve lang key to text for the response
            string resolvedMessage = LocalizationUtils.GetSafe(msgKey);
            if (string.IsNullOrEmpty(resolvedMessage) || resolvedMessage == msgKey)
            {
                resolvedMessage = Lang.Get(msgKey);
            }

            var serverChannel = player.Entity.Api.Network.GetChannel(ChannelName) as IServerNetworkChannel;
            serverChannel?.SendPacket(new PromoCodeResultMessage
            {
                Success = success,
                Message = resolvedMessage
            }, player);
        }

        /// <summary>
        /// Send a message to a player to open the promo code GUI.
        /// </summary>
        public static void SendOpenGui(ICoreServerAPI sapi, IServerPlayer player)
        {
            var channel = sapi.Network.GetChannel(ChannelName);
            channel?.SendPacket(new ShowPromoCodeGuiMessage(), player);
        }

        #endregion

        #region Client Side

        public void RegisterClient(ICoreClientAPI capi)
        {
            this.capi = capi;

            capi.Network.RegisterChannel(ChannelName)
                .RegisterMessageType<RedeemPromoCodeMessage>()
                .RegisterMessageType<PromoCodeResultMessage>()
                .SetMessageHandler<PromoCodeResultMessage>(OnResultMessage)
                .RegisterMessageType<ShowPromoCodeGuiMessage>()
                .SetMessageHandler<ShowPromoCodeGuiMessage>(OnShowGuiMessage);
        }

        private void OnResultMessage(PromoCodeResultMessage message)
        {
            if (activeGui == null || !activeGui.IsOpened()) return;

            activeGui.ShowResult(message.Message, message.Success);

            if (message.Success)
            {
                activeGui.ClearInput();
                // Play success sound — rewarding chime
                capi.Gui.PlaySound(new AssetLocation("sounds/effect/receptionbell"), false, 0.5f);
            }
            else
            {
                // Play error sound — subtle deny
                capi.Gui.PlaySound(new AssetLocation("sounds/effect/writing"), false, 0.4f);
            }
        }

        private void OnShowGuiMessage(ShowPromoCodeGuiMessage message)
        {
            OpenGui();
            // Play open sound — paper unfold
            capi.Gui.PlaySound(new AssetLocation("sounds/effect/writing"), false, 0.3f);
        }

        /// <summary>
        /// Open the promo code GUI on the client.
        /// Can be called from a client-side command or from a network message.
        /// </summary>
        public void OpenGui()
        {
            if (capi == null) return;

            if (activeGui != null && activeGui.IsOpened())
            {
                activeGui.TryClose();
            }

            activeGui = new PromoCodeGui(capi);
            activeGui.TryOpen();
        }

        #endregion
    }
}
