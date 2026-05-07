using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class BlockBossHuntAnchor : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world == null || byPlayer == null || blockSel == null) return false;

            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBossHuntAnchor;
            if (be == null) return false;

            if (world.Side == EnumAppSide.Server)
            {
                var sp = byPlayer as IServerPlayer;
                if (sp == null || !sp.HasPrivilege(Privilege.controlserver)) return true;
            }

            be.OnInteract(byPlayer);
            return true;
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            if (world?.Side == EnumAppSide.Server)
            {
                try
                {
                    var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBossHuntAnchor;
                    be?.OnRemovedServerSide();
                }
                catch (Exception ex)
                {
                    world?.Api?.Logger?.Error($"[vsquest] Exception in OnBlockRemoved: {ex}");
                }
            }

            base.OnBlockRemoved(world, pos);

            if (world?.BlockAccessor == null || EntityClass == null) return;
            world.BlockAccessor.RemoveBlockEntity(pos);
        }
    }
}
