using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestAttributeChatCommandSubRegistry : IChatCommandSubRegistry
    {
        public void Register(IChatCommand avq, ICoreServerAPI sapi)
        {
            var questAttrSetHandler = new QuestAttrSetCommandHandler(sapi);
            var questAttrRemoveHandler = new QuestAttrRemoveCommandHandler(sapi);
            var questAttrListHandler = new QuestAttrListCommandHandler(sapi);
            var questWAttrHandler = new QuestWAttrCommandHandler(sapi);

            avq.BeginSubCommand("attr")
                .WithDescription("Admin player attributes.")
                .RequiresPrivilege(Privilege.give)
                .BeginSubCommand("set")
                    .WithDescription("Sets a string attribute on an online player.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(
                        sapi.ChatCommands.Parsers.Word("playerName"),
                        sapi.ChatCommands.Parsers.Word("key"),
                        sapi.ChatCommands.Parsers.Word("value")
                    )
                    .HandleWith(questAttrSetHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("list")
                    .WithDescription("Lists watched attributes for an online player.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(
                        sapi.ChatCommands.Parsers.Word("playerName")
                    )
                    .HandleWith(questAttrListHandler.Handle)
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithDescription("Removes an attribute from an online player.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(
                        sapi.ChatCommands.Parsers.Word("playerName"),
                        sapi.ChatCommands.Parsers.Word("key")
                    )
                    .HandleWith(questAttrRemoveHandler.Handle)
                .EndSubCommand()
            .EndSubCommand()
            .BeginSubCommand("wattr")
                .WithDescription("Admin WatchedAttributes on an online player. If no player is given, uses the caller.")
                .RequiresPrivilege(Privilege.give)
                .BeginSubCommand("setint")
                    .WithDescription("Sets an int WatchedAttribute.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(
                        sapi.ChatCommands.Parsers.OptionalWord("playerName"),
                        sapi.ChatCommands.Parsers.Word("key"),
                        sapi.ChatCommands.Parsers.Int("value")
                    )
                    .HandleWith(questWAttrHandler.SetInt)
                .EndSubCommand()
                .BeginSubCommand("setfloat")
                    .WithDescription("Sets a float WatchedAttribute.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(
                        sapi.ChatCommands.Parsers.OptionalWord("playerName"),
                        sapi.ChatCommands.Parsers.Word("key"),
                        sapi.ChatCommands.Parsers.Word("value")
                    )
                    .HandleWith(questWAttrHandler.SetFloat)
                .EndSubCommand()
                .BeginSubCommand("addint")
                    .WithDescription("Adds delta to an int WatchedAttribute.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(
                        sapi.ChatCommands.Parsers.OptionalWord("playerName"),
                        sapi.ChatCommands.Parsers.Word("key"),
                        sapi.ChatCommands.Parsers.Int("delta")
                    )
                    .HandleWith(questWAttrHandler.AddInt)
                .EndSubCommand()
                .BeginSubCommand("addfloat")
                    .WithDescription("Adds delta to a float WatchedAttribute.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(
                        sapi.ChatCommands.Parsers.OptionalWord("playerName"),
                        sapi.ChatCommands.Parsers.Word("key"),
                        sapi.ChatCommands.Parsers.Word("delta")
                    )
                    .HandleWith(questWAttrHandler.AddFloat)
                .EndSubCommand()
                .BeginSubCommand("setbool")
                    .WithDescription("Sets a bool WatchedAttribute.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(
                        sapi.ChatCommands.Parsers.OptionalWord("playerName"),
                        sapi.ChatCommands.Parsers.Word("key"),
                        sapi.ChatCommands.Parsers.Bool("value")
                    )
                    .HandleWith(questWAttrHandler.SetBool)
                .EndSubCommand()
                .BeginSubCommand("setstring")
                    .WithDescription("Sets a string WatchedAttribute.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(
                        sapi.ChatCommands.Parsers.OptionalWord("playerName"),
                        sapi.ChatCommands.Parsers.Word("key"),
                        sapi.ChatCommands.Parsers.All("value")
                    )
                    .HandleWith(questWAttrHandler.SetString)
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithDescription("Removes a WatchedAttribute key.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(
                        sapi.ChatCommands.Parsers.OptionalWord("playerName"),
                        sapi.ChatCommands.Parsers.Word("key")
                    )
                    .HandleWith(questWAttrHandler.Remove)
                .EndSubCommand()
            .EndSubCommand()
            .BeginSubCommand("fixplayer")
                .WithDescription("Clears common boss debuffs and stuck watched attributes on an online player.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(
                    sapi.ChatCommands.Parsers.OptionalWord("playerName")
                )
                .HandleWith(questWAttrHandler.FixPlayer)
            .EndSubCommand();
        }
    }
}
