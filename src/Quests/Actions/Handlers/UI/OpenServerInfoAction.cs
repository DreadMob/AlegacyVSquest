using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class OpenServerInfoAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null || message == null || byPlayer == null) return;

            sapi.Network.GetChannel(VsQuestNetworkRegistry.QuestChannelName)
                .SendPacket(new ShowServerInfoMessage(), byPlayer);
        }
    }
}
