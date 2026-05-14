using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest
{
    /// <summary>
    /// Client-side system for Hollow Trials.
    /// Registers network channels for shop GUI.
    /// </summary>
    public class HollowTrialClientSystem : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI capi)
        {
            var shopHandler = new TrialShopNetworkHandler();
            shopHandler.RegisterClient(capi);
        }
    }
}
