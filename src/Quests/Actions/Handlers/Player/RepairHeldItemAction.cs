using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Repairs the item in the player's right hand by a specified amount.
    /// Works on ANY item with durability (tools, armor, wearables, etc.)
    /// Args: [amount]
    /// Example dialogue trigger: "repairheldany 2000"
    /// </summary>
    public class RepairHeldItemAction : PlayerActionBase
    {
        protected override int MinArgs => 1;
        protected override string ActionName => "repairheldany";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            if (!int.TryParse(args[0], out int amount) || amount <= 0)
            {
                sapi.Logger.Error($"[vsquest] 'repairheldany' action has an invalid amount '{args[0]}'.");
                return;
            }

            var slot = player.Entity.RightHandItemSlot;
            if (slot == null || slot.Empty) return;

            var stack = slot.Itemstack;
            var collectible = stack.Collectible;

            int currentDura = collectible.GetRemainingDurability(stack);
            int maxDura = collectible.GetMaxDurability(stack);

            // Only repair if item has durability and is damaged
            if (maxDura <= 0 || currentDura >= maxDura) return;

            int newDura = Math.Min(maxDura, currentDura + amount);
            collectible.SetDurability(stack, newDura);
            slot.MarkDirty();
        }
    }
}
