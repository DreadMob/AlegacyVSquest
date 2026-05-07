using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest
{
    public class HasItemObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string itemCode, out int need)) return false;
            return CountItems(byPlayer, itemCode) >= need;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string itemCode, out int need)) return new List<int>(new int[] { 0, 0 });

            int have = CountItems(byPlayer, itemCode);
            if (have > need) have = need;
            if (need < 0) need = 0;

            return new List<int>(new int[] { have, need });
        }

        private static bool TryParseArgs(string[] args, out string itemCode, out int need)
        {
            itemCode = null;
            need = 0;

            if (args == null || args.Length < 2) return false;

            itemCode = args[0];
            if (string.IsNullOrWhiteSpace(itemCode)) return false;

            if (!int.TryParse(args[1], out need)) need = 0;
            if (need < 0) need = 0;

            return true;
        }

        private static int CountItems(IPlayer byPlayer, string itemCode)
        {
            if (byPlayer?.InventoryManager?.Inventories == null) return 0;

            int itemsFound = 0;
            foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
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
