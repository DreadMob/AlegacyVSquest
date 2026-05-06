using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestActionItemChatCommandSubRegistry : IChatCommandSubRegistry
    {
        private readonly ICoreAPI api;

        public QuestActionItemChatCommandSubRegistry(ICoreAPI api)
        {
            this.api = api;
        }

        public void Register(IChatCommand avq, ICoreServerAPI sapi)
        {
            var itemSystem = api.ModLoader.GetModSystem<ItemSystem>();
            var getActionItemHandler = new GetActionItemCommandHandler(api, itemSystem);
            var questActionItemsHandler = new QuestActionItemsCommandHandler(itemSystem);
            var actionItemDurabilityHandler = new ActionItemDurabilityCommandHandler();

            avq.BeginSubCommand("actionitems")
                .WithDescription("Lists all registered action items.")
                .RequiresPrivilege(Privilege.give)
                .HandleWith(questActionItemsHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("getactionitem")
                .WithDescription("Gives a player an action item defined in itemconfig.json.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(sapi.ChatCommands.Parsers.Word("itemId"), sapi.ChatCommands.Parsers.OptionalInt("amount", 1))
                .HandleWith(getActionItemHandler.Handle)
            .EndSubCommand()
            .BeginSubCommand("ai")
                .WithDescription("Action item durability tools")
                .RequiresPrivilege(Privilege.give)
                .BeginSubCommand("repair")
                    .WithDescription("Repair held item to max durability.")
                    .RequiresPrivilege(Privilege.give)
                    .HandleWith(actionItemDurabilityHandler.Repair)
                .EndSubCommand()
                .BeginSubCommand("destruct")
                    .WithDescription("Damage held item by a value.")
                    .RequiresPrivilege(Privilege.give)
                    .WithArgs(sapi.ChatCommands.Parsers.Int("amount"))
                    .HandleWith(actionItemDurabilityHandler.Destruct)
                .EndSubCommand()
            .EndSubCommand();
        }
    }
}
