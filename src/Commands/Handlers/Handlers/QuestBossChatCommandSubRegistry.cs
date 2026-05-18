using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestBossChatCommandSubRegistry : IChatCommandSubRegistry
    {
        public void Register(IChatCommand avq, ICoreServerAPI sapi)
        {
            var bossHuntSkipHandler = new BossHuntSkipCommandHandler(sapi);
            var bossHuntStatusHandler = new BossHuntStatusCommandHandler(sapi);
            var bossHuntReloadHandler = new BossHuntReloadCommandHandler(sapi);
            var bossClearCorpseHandler = new BossClearCorpseCommandHandler(sapi);
            var debugBaHandler = new DebugBossAbilityCommandHandler(sapi);

            avq.BeginSubCommand("bosshunt")
                .WithDescription("Bosshunt admin commands")
                .RequiresPrivilege(Privilege.give)
                .BeginSubCommand("skip")
                    .WithDescription("Force-rotates the active bosshunt target to the next entry.")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(bossHuntSkipHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("status")
                    .WithDescription("Shows the current bosshunt target and time until rotation.")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(bossHuntStatusHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("reload")
                    .WithDescription("Clears saved bosshunt anchors and re-registers anchors from loaded chunks (scan around online players).")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.OptionalInt("radiusBlocks", 512))
                    .HandleWith(bossHuntReloadHandler.Handle)
                .EndSubCommand()
            .EndSubCommand()
            .BeginSubCommand("clearcorpse")
                .WithDescription("Despawns dead boss corpses from regular boss encounters (not bosshunt). Radius 0 = all loaded chunks.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(sapi.ChatCommands.Parsers.OptionalInt("radiusBlocks", 0))
                .HandleWith(bossClearCorpseHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("debugba")
                .WithDescription("Debug boss abilities — toggle mode where only one ability fires. Use next/prev to cycle.")
                .RequiresPrivilege(Privilege.give)
                .HandleWith(debugBaHandler.Toggle)
                .BeginSubCommand("next")
                    .WithDescription("Switch to the next ability on the boss")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(debugBaHandler.Next)
                .EndSubCommand()
                .BeginSubCommand("prev")
                    .WithDescription("Switch to the previous ability on the boss")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(debugBaHandler.Prev)
                .EndSubCommand()
                .BeginSubCommand("set")
                    .WithDescription("Set a specific ability by name")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.Word("abilityName"))
                    .HandleWith(debugBaHandler.Set)
                .EndSubCommand()
                .BeginSubCommand("fire")
                    .WithDescription("Force-fire the currently selected ability immediately")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(debugBaHandler.Fire)
                .EndSubCommand()
                .BeginSubCommand("list")
                    .WithDescription("List all abilities on the nearest boss")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(debugBaHandler.List)
                .EndSubCommand()
                .BeginSubCommand("resetcd")
                    .WithDescription("Reset all cooldowns on the nearest boss")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(debugBaHandler.ResetCooldowns)
                .EndSubCommand()
                .BeginSubCommand("enrage")
                    .WithDescription("Toggle enrage on the nearest boss")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(debugBaHandler.Enrage)
                .EndSubCommand()
            .EndSubCommand();
        }
    }
}
