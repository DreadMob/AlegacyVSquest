using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestAdminChatCommandSubRegistry : IChatCommandSubRegistry
    {
        private readonly QuestSystem questSystem;

        public QuestAdminChatCommandSubRegistry(QuestSystem questSystem)
        {
            this.questSystem = questSystem;
        }

        public void Register(IChatCommand avq, ICoreServerAPI sapi)
        {
            var reloadHandler = new QuestReloadCommandHandler(sapi, questSystem);
            var profilerHandler = new QuestProfilerCommandHandler(sapi);
            var fullDietHandler = new FullDietCommandHandler(sapi);
            var healHandler = new HealCommandHandler(sapi);
            var debugDamageHandler = new DebugDamageCommandHandler(sapi);
            var questExecActionStringHandler = new QuestExecActionStringCommandHandler(sapi);

            avq.BeginSubCommand("reload")
                .WithDescription("Reloads mod configs (questconfig.json, alegacy-vsquest-config.json). Does not reload assets.")
                .RequiresPrivilege(Privilege.give)
                .HandleWith(reloadHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("fulldiet")
                .WithDescription("Sets a player's nutrition to full for all categories.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playerName"))
                .HandleWith(fullDietHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("heal")
                .WithDescription("Heals a player to full health.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playerName"))
                .HandleWith(healHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("exec")
                .WithDescription("Executes an action string (ActionStringExecutor) on a player. If no player is given, uses the caller.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(
                    sapi.ChatCommands.Parsers.OptionalWord("playerName"),
                    sapi.ChatCommands.Parsers.All("actionString")
                )
                .HandleWith(questExecActionStringHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("profiler")
                .WithDescription("Performance profiler commands")
                .RequiresPrivilege(Privilege.give)
                .BeginSubCommand("enable")
                    .WithDescription("Enables performance profiling")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(profilerHandler.Enable)
                .EndSubCommand()
                .BeginSubCommand("disable")
                    .WithDescription("Disables performance profiling")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(profilerHandler.Disable)
                .EndSubCommand()
                .BeginSubCommand("status")
                    .WithDescription("Shows profiler status")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(profilerHandler.Status)
                .EndSubCommand()
                .BeginSubCommand("clear")
                    .WithDescription("Clears profiler statistics")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(profilerHandler.Clear)
                .EndSubCommand()
            .EndSubCommand()
            .BeginSubCommand("debugdamage")
                .WithDescription("Debug damage logging - shows damage you deal to entities in chat")
                .RequiresPrivilege(Privilege.give)
                .BeginSubCommand("enable")
                    .WithDescription("Enable damage debug messages")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(debugDamageHandler.Enable)
                .EndSubCommand()
                .BeginSubCommand("disable")
                    .WithDescription("Disable damage debug messages")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(debugDamageHandler.Disable)
                .EndSubCommand()
                .BeginSubCommand("status")
                    .WithDescription("Show damage debug status")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(debugDamageHandler.Status)
                .EndSubCommand()
            .EndSubCommand();
        }
    }
}
