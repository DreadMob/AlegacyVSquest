using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class GiveItemAction : PlayerActionBase
    {
        protected override int MinArgs => 2;
        protected override string ActionName => "giveitem";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            string code = args[0];
            if (!int.TryParse(args[1], out int amount))
            {
                sapi.Logger.Error($"[vsquest] Invalid amount '{args[1]}' for 'giveitem' action.");
                return;
            }

            CollectibleObject item = sapi.World.GetItem(new AssetLocation(code)) ?? (CollectibleObject)sapi.World.GetBlock(new AssetLocation(code));
            if (item == null)
            {
                sapi.Logger.Error($"[vsquest] Could not find item or block {code} for 'giveitem' action.");
                return;
            }

            var stack = new ItemStack(item, amount);

            if (args.Length > 2)
            {
                stack.Attributes.SetString(ItemAttributeUtils.QuestNameKey, args[2]);
            }
            if (args.Length > 3)
            {
                string desc = string.Join(" ", args, 3, args.Length - 3);
                stack.Attributes.SetString(ItemAttributeUtils.QuestDescKey, desc);
            }

            if (!player.InventoryManager.TryGiveItemstack(stack))
            {
                sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
            }
        }
    }
}
