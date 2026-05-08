using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestNetworkChannelRegistry
    {
        private readonly QuestSystem questSystem;

        public QuestNetworkChannelRegistry(QuestSystem questSystem)
        {
            this.questSystem = questSystem;
        }

        public void RegisterClient(ICoreClientAPI capi)
        {
            VsQuestNetworkRegistry.RegisterQuestClient(capi, questSystem);
            VsQuestNetworkRegistry.RegisterRerollClient(capi, questSystem);
        }

        public void RegisterServer(ICoreServerAPI sapi)
        {
            VsQuestNetworkRegistry.RegisterQuestServer(sapi, questSystem);
            VsQuestNetworkRegistry.RegisterRerollServer(sapi, questSystem);
        }
    }
}
