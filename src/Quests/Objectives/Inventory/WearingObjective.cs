using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class WearingObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string itemCode, out int? slotIndex)) return false;
            return IsWearing(byPlayer, itemCode, slotIndex);
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string itemCode, out int? slotIndex)) return new List<int>(new int[] { 0, 0 });

            bool ok = IsWearing(byPlayer, itemCode, slotIndex);
            return ok
                ? new List<int>(new int[] { 1, 1 })
                : new List<int>(new int[] { 0, 1 });
        }

        private static bool TryParseArgs(string[] args, out string itemCode, out int? slotIndex)
        {
            itemCode = null;
            slotIndex = null;

            if (args == null || args.Length < 1) return false;

            itemCode = args[0];
            if (string.IsNullOrWhiteSpace(itemCode)) return false;

            if (args.Length >= 2 && !string.IsNullOrWhiteSpace(args[1]) && int.TryParse(args[1], out int parsed))
            {
                slotIndex = parsed;
            }

            return true;
        }

        private static bool IsWearing(IPlayer byPlayer, string itemCode, int? slotIndex)
        {
            if (byPlayer?.InventoryManager == null) return false;

            IInventory inv = byPlayer.InventoryManager.GetOwnInventory("character");
            if (inv == null) return false;

            if (slotIndex.HasValue)
            {
                int idx = slotIndex.Value;
                if (idx < 0 || idx >= inv.Count) return false;
                var slot = inv[idx];
                return SlotMatches(slot, itemCode);
            }

            foreach (var slot in inv)
            {
                if (SlotMatches(slot, itemCode)) return true;
            }

            return false;
        }

        private static bool SlotMatches(ItemSlot slot, string itemCode)
        {
            if (slot?.Empty != false) return false;
            var stack = slot.Itemstack;
            if (stack?.Collectible?.Code == null) return false;

            string code = stack.Collectible.Code.ToString();
            if (itemCode.EndsWith("*") && code.StartsWith(itemCode.Substring(0, itemCode.Length - 1), StringComparison.OrdinalIgnoreCase)) return true;

            return string.Equals(code, itemCode, StringComparison.OrdinalIgnoreCase);
        }
    }
}
