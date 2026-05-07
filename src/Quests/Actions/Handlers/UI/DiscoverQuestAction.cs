using Vintagestory.API.Server;

namespace VsQuest
{
    public class DiscoverQuestAction : PlayerActionBase
    {
        protected override int MinArgs => 1;
        protected override string ActionName => "discover";

        protected override void Execute(ICoreServerAPI api, IServerPlayer byPlayer, string[] args)
        {
            api.Network.GetChannel("alegacyvsquest").SendPacket(new ShowDiscoveryMessage() { Notification = args[0] }, byPlayer);
        }
    }
}
