using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestChatCommandRegistry
    {
        private readonly ICoreServerAPI sapi;
        private readonly ICoreAPI api;
        private readonly QuestSystem questSystem;

        public QuestChatCommandRegistry(ICoreServerAPI sapi, ICoreAPI api, QuestSystem questSystem)
        {
            this.sapi = sapi;
            this.api = api;
            this.questSystem = questSystem;
        }

        public void Register()
        {
            var avq = sapi.ChatCommands.GetOrCreate("avq")
                .WithDescription("Quest administration commands")
                .RequiresPrivilege(Privilege.give);

            var subRegistries = new IChatCommandSubRegistry[]
            {
                new QuestAdminChatCommandSubRegistry(questSystem),
                new QuestManagementChatCommandSubRegistry(questSystem),
                new QuestActionItemChatCommandSubRegistry(api),
                new QuestEntityChatCommandSubRegistry(questSystem),
                new QuestAttributeChatCommandSubRegistry(),
                new QuestBossChatCommandSubRegistry()
            };

            foreach (var subRegistry in subRegistries)
            {
                subRegistry.Register(avq, sapi);
            }
        }
    }
}
