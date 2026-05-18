using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class BlockVoidRiftAnchor : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world == null || byPlayer == null || blockSel == null) return false;

            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityVoidRiftAnchor;
            if (be == null) return false;

            if (world.Side == EnumAppSide.Server)
            {
                var sp = byPlayer as IServerPlayer;
                if (sp == null) return true;

                // Shift+click with admin privilege opens config GUI
                if (sp.HasPrivilege(Privilege.controlserver) && byPlayer.Entity?.Controls?.ShiftKey == true)
                {
                    be.OnInteract(byPlayer);
                    return true;
                }

                // Any player RMB: summon boss
                be.OnPlayerSummonRequest(byPlayer);
                return true;
            }

            // Client side: admin shift+click opens GUI
            if (byPlayer.Entity?.Controls?.ShiftKey == true)
            {
                be.OnInteract(byPlayer);
            }
            return true;
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            if (world?.Side == EnumAppSide.Server)
            {
                var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityVoidRiftAnchor;
                be?.OnRemovedServerSide();
            }

            base.OnBlockRemoved(world, pos);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (world?.Side == EnumAppSide.Server)
            {
                var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityVoidRiftAnchor;
                be?.OnRemovedServerSide();
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }
    }
}
