using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Quest action that opens the reroll dialog for a player.
    /// Usage: "openrerolldialog" with no arguments.
    /// </summary>
    public class OpenRerollDialogAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var itemSystem = sapi.ModLoader.GetModSystem<ItemSystem>();
            var rerollService = itemSystem?.RerollService;
            if (rerollService == null) return;

            // Get all groups with item counts
            var allGroups = rerollService.GetAllGroups();
            var counts = rerollService.CountItemsByGroup(byPlayer);
            var groupStrings = new List<string>();

            foreach (var group in allGroups)
            {
                int itemCount = 0;
                counts.TryGetValue(group.id, out itemCount);

                // Get first reward item code for icon
                string iconCode = "";
                if (group.rewardItems != null && group.rewardItems.Count > 0)
                {
                    if (itemSystem.ActionItemRegistry.TryGetValue(group.rewardItems[0], out var actionItem))
                    {
                        iconCode = actionItem.itemCode ?? "";
                    }
                }

                groupStrings.Add($"{group.id}|{group.name}|{itemCount}|{group.itemsRequired}|{iconCode}");
            }

            sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowRerollDialogMessage
            {
                AvailableGroups = groupStrings.ToArray()
            }, byPlayer);
        }
    }
}
