using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class BlockQuestSpawner : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world == null || byPlayer == null || blockSel == null) return false;

            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityQuestSpawner;
            if (be == null) return false;

            if (world.Side == EnumAppSide.Server)
            {
                var sp = byPlayer as IServerPlayer;
                if (sp == null || !sp.HasPrivilege(Privilege.controlserver)) return true;
            }

            // Optional quick toggle without opening UI
            if (world.Side == EnumAppSide.Server && byPlayer.Entity?.Controls?.Sneak == true)
            {
                be.ToggleEnabled();
                return true;
            }

            // Quick add from item (creative helper)
            var slot = byPlayer.InventoryManager?.ActiveHotbarSlot;
            var stack = slot?.Itemstack;
            if (world.Side == EnumAppSide.Server && stack != null)
            {
                bool added = false;
                string entityCode = null;

                // Check for custom entity spawner item
                if (stack.Collectible?.Code != null && stack.Collectible.Code.ToShortString() == "alegacyvsquest:entityspawner")
                {
                    added = be.TryAppendFromEntitySpawnerItem(stack);
                }
                // Check for vanilla spawn eggs or items with entity codes
                else if (stack.Attributes != null)
                {
                    // Try common attribute names for entity codes
                    entityCode = stack.Attributes.GetString("entityCode", null)
                        ?? stack.Attributes.GetString("type", null)
                        ?? stack.Attributes.GetString("creature", null);

                    if (!string.IsNullOrWhiteSpace(entityCode))
                    {
                        added = be.TryAppendEntityCode(entityCode);
                    }
                }

                if (stack.Collectible?.Code != null || entityCode != null)
                {
                    var sapi = world.Api as Vintagestory.API.Server.ICoreServerAPI;
                    if (sapi != null)
                    {
                        sapi.SendMessage(byPlayer as Vintagestory.API.Server.IServerPlayer, GlobalConstants.InfoLogChatGroup, Lang.Get(added ? "alegacyvsquest:questspawner-added-entry" : "alegacyvsquest:questspawner-entry-exists"), EnumChatType.Notification);
                    }
                    return true;
                }
            }

            be.OnInteract(byPlayer);
            return true;
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            base.OnBlockRemoved(world, pos);

            if (world?.BlockAccessor == null || EntityClass == null) return;
            world.BlockAccessor.RemoveBlockEntity(pos);
        }
    }
}
