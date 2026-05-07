using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class TakeItemAction : PlayerActionBase
    {
        protected override int MinArgs => 2;
        protected override string ActionName => "takeitem";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            if (player.InventoryManager?.Inventories == null) return;

            string itemCode = args[0];
            if (string.IsNullOrWhiteSpace(itemCode)) return;

            if (!int.TryParse(args[1], out int amount))
            {
                sapi.Logger.Error($"[vsquest] Invalid amount '{args[1]}' for 'takeitem' action.");
                return;
            }

            if (amount <= 0) return;

            int have = CountItems(player, itemCode);
            if (have < amount)
            {
                sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, Lang.Get("alegacyvsquest:takeitem-not-enough"), EnumChatType.Notification);
                return;
            }

            int remaining = amount;

            foreach (var inv in player.InventoryManager.Inventories.Values)
            {
                if (inv == null) continue;
                if (inv.ClassName == GlobalConstants.creativeInvClassName) continue;

                int slotCount = inv.Count;

                for (int i = 0; i < slotCount; i++)
                {
                    if (remaining <= 0) return;

                    var slot = inv[i];
                    if (slot?.Empty != false) continue;

                    var stack = slot.Itemstack;
                    if (stack?.Collectible?.Code == null) continue;

                    string code = stack.Collectible.Code.ToString();
                    if (!CodeMatches(itemCode, code)) continue;

                    int take = Math.Min(remaining, stack.StackSize);
                    slot.TakeOut(take);
                    slot.MarkDirty();

                    remaining -= take;
                }
            }
        }

        private static int CountItems(IServerPlayer player, string itemCode)
        {
            if (player.InventoryManager?.Inventories == null) return 0;

            int itemsFound = 0;
            foreach (var inventory in player.InventoryManager.Inventories.Values)
            {
                if (inventory == null) continue;
                if (inventory.ClassName == GlobalConstants.creativeInvClassName) continue;

                foreach (var slot in inventory)
                {
                    if (slot?.Empty != false) continue;
                    var stack = slot.Itemstack;
                    if (stack?.Collectible?.Code == null) continue;

                    string code = stack.Collectible.Code.ToString();
                    if (CodeMatches(itemCode, code))
                    {
                        itemsFound += stack.StackSize;
                    }
                }
            }

            return itemsFound;
        }

        private static bool CodeMatches(string expected, string actual)
        {
            if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual)) return false;

            expected = expected.Trim();
            actual = actual.Trim();

            if (expected.EndsWith("*") && actual.StartsWith(expected.Substring(0, expected.Length - 1), StringComparison.OrdinalIgnoreCase)) return true;

            return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }
    }
}
