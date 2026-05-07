using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestManagementChatCommandSubRegistry : IChatCommandSubRegistry
    {
        private readonly QuestSystem questSystem;

        public QuestManagementChatCommandSubRegistry(QuestSystem questSystem)
        {
            this.questSystem = questSystem;
        }

        public void Register(IChatCommand avq, ICoreServerAPI sapi)
        {
            var questListHandler = new QuestListCommandHandler(sapi, questSystem);
            var questCheckHandler = new QuestCheckCommandHandler(sapi, questSystem);
            var questStartHandler = new QuestStartCommandHandler(sapi, questSystem);
            var questCompleteHandler = new QuestCompleteCommandHandler(sapi, questSystem);
            var questCompleteActiveHandler = new QuestCompleteActiveCommandHandler(sapi, questSystem);
            var questSkipStageHandler = new QuestSkipStageCommandHandler(sapi, questSystem);
            var forgiveQuestHandler = new QuestForgiveCommandHandler(sapi, questSystem);
            var questForgiveActiveAliasHandler = new QuestForgiveAliasCommandHandler(sapi, questSystem, "active");
            var questForgiveAllAliasHandler = new QuestForgiveAliasCommandHandler(sapi, questSystem, "all");
            var questNoteForgiveHandler = new QuestNoteForgiveCommandHandler(sapi);

            avq.BeginSubCommand("qlist")
                .WithDescription("Lists all registered quest IDs and their titles.")
                .RequiresPrivilege(Privilege.give)
                .HandleWith(questListHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("qcheck")
                .WithDescription("Shows active/completed quests and progress for a player.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(sapi.ChatCommands.Parsers.Word("playerName"))
                .HandleWith(questCheckHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("qstart")
                .WithDescription("Starts a quest for a player.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(sapi.ChatCommands.Parsers.Word("questId"), sapi.ChatCommands.Parsers.Word("playerName"))
                .HandleWith(questStartHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("qcomplete")
                .WithDescription("Force-completes an active quest for a player.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(sapi.ChatCommands.Parsers.Word("questId"), sapi.ChatCommands.Parsers.Word("playerName"))
                .HandleWith(questCompleteHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("qcompleteactive")
                .WithDescription("Force-completes the player's currently active quest.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playerName"))
                .HandleWith(questCompleteActiveHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("qca")
                .WithDescription("Alias for qcompleteactive.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playerName"))
                .HandleWith(questCompleteActiveHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("skipstage")
                .WithDescription("Skips the current stage of a quest for a player. If no player is given, uses the caller. If no questId is given, uses the active quest.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(
                    sapi.ChatCommands.Parsers.OptionalWord("playerName"),
                    sapi.ChatCommands.Parsers.OptionalWord("questId")
                )
                .HandleWith(questSkipStageHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("qforgive")
                .WithDescription("Resets a quest for a player: removes it from active quests and clears cooldown/completed flags.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(
                    sapi.ChatCommands.Parsers.Word("modeOrQuestId"),
                    sapi.ChatCommands.Parsers.OptionalWord("playerName")
                )
                .HandleWith(forgiveQuestHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("qfa")
                .WithDescription("Alias for qforgive active.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playerName"))
                .HandleWith(questForgiveActiveAliasHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("qfall")
                .WithDescription("Alias for qforgive all.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playerName"))
                .HandleWith(questForgiveAllAliasHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("nforgive")
                .WithDescription("Removes all note entries from the journal for a player.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playerName"))
                .HandleWith(questNoteForgiveHandler.Handle)
            .EndSubCommand();
        }
    }
}
