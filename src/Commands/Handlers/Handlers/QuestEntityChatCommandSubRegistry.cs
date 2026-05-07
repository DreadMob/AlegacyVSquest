using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestEntityChatCommandSubRegistry : IChatCommandSubRegistry
    {
        private readonly QuestSystem questSystem;

        public QuestEntityChatCommandSubRegistry(QuestSystem questSystem)
        {
            this.questSystem = questSystem;
        }

        public void Register(IChatCommand avq, ICoreServerAPI sapi)
        {
            var questNpcListHandler = new QuestNpcListCommandHandler(sapi);
            var questEntityHandler = new QuestEntityCommandHandler(sapi, questSystem);

            avq.BeginSubCommand("entities")
                .WithDescription("Quest entity tools")
                .RequiresPrivilege(Privilege.give)
                .BeginSubCommand("spawned")
                    .WithDescription("Lists loaded questgiver NPCs (entity id, code, position).")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(questNpcListHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("all")
                    .WithDescription("Lists entity types from a quest pack domain (assets/<domain>/entities).")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(questEntityHandler.Handle)
                .EndSubCommand()
            .EndSubCommand();
        }
    }
}
