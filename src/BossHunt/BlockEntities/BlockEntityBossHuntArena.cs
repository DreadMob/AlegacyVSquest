using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class BlockEntityBossHuntArena : BlockEntity
    {
        private const string AttrYOffset = "alegacyvsquest:bosshuntarena:yOffset";
        private const string AttrKeepInventory = "alegacyvsquest:bosshuntarena:keepInventory";
        private const string AttrHealingReduction = "alegacyvsquest:bosshuntarena:healingReduction";

        private const int PacketOpenGui = 3000;
        private const int PacketSave = 3001;

        private float yOffset;
        private bool keepInventory;
        private float healingReduction = 0.35f;

        private long healSuppressionTickId = -1;

        private BossHuntArenaConfigGui dlg;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (Api?.Side == EnumAppSide.Server)
            {
                TryRegister();
                RegisterHealSuppressionTick();
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (Api?.Side != EnumAppSide.Server) return;

            var attrs = Block?.Attributes;
            yOffset = attrs?["yOffset"].AsFloat(yOffset) ?? yOffset;
            keepInventory = attrs?["keepInventory"].AsBool(keepInventory) ?? keepInventory;

            TryRegister();
            MarkDirty(true);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            yOffset = tree.GetFloat(AttrYOffset, yOffset);
            keepInventory = tree.GetBool(AttrKeepInventory, keepInventory);

            if (Api?.Side == EnumAppSide.Server)
            {
                TryRegister();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetFloat(AttrYOffset, yOffset);
            tree.SetBool(AttrKeepInventory, keepInventory);
        }

        internal void OnInteract(IPlayer byPlayer)
        {
            if (byPlayer == null) return;

            if (Api.Side == EnumAppSide.Server)
            {
                var sp = byPlayer as IServerPlayer;
                if (sp == null) return;

                var data = BuildConfigData();
                (Api as ICoreServerAPI).Network.SendBlockEntityPacket(sp, Pos, PacketOpenGui, SerializerUtil.Serialize(data));
                return;
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] bytes)
        {
            if (packetid != PacketOpenGui) return;

            var data = SerializerUtil.Deserialize<BossHuntArenaConfigData>(bytes);

            if (dlg == null || !dlg.IsOpened())
            {
                dlg = new BossHuntArenaConfigGui(Pos, Api as Vintagestory.API.Client.ICoreClientAPI);
                dlg.Data = data;
                dlg.TryOpen();
                dlg.OnClosed += () =>
                {
                    dlg?.Dispose();
                    dlg = null;
                };
            }
            else
            {
                dlg.UpdateFromServer(data);
            }

            ApplyConfigData(data, markDirty: false);
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] bytes)
        {
            var sp = fromPlayer as IServerPlayer;
            if (sp == null || !sp.HasPrivilege(Privilege.controlserver)) return;

            if (packetid != PacketSave) return;

            var data = SerializerUtil.Deserialize<BossHuntArenaConfigData>(bytes);
            ApplyConfigData(data, markDirty: true);

            var refreshed = BuildConfigData();
            (Api as ICoreServerAPI).Network.SendBlockEntityPacket(sp, Pos, PacketOpenGui, SerializerUtil.Serialize(refreshed));
        }

        internal void OnRemovedServerSide()
        {
            if (Api?.Side != EnumAppSide.Server) return;

            UnregisterHealSuppressionTick();
            ClearHealSuppression();

            try
            {
                var system = Api.ModLoader.GetModSystem<BossHuntArenaSystem>();
                system?.UnregisterArena(new BlockPos(Pos.X, Pos.Y, Pos.Z, Pos.dimension));
            }
            catch (Exception ex)
            {
                Api?.Logger?.Error($"[vsquest] Exception in UnregisterArena: {ex}");
            }
        }

        private void RegisterHealSuppressionTick()
        {
            if (Api?.Side != EnumAppSide.Server) return;
            if (healSuppressionTickId >= 0) return;
            healSuppressionTickId = Api.Event.RegisterGameTickListener(OnHealSuppressionTick, 2000);
        }

        private void UnregisterHealSuppressionTick()
        {
            if (healSuppressionTickId >= 0)
            {
                Api.Event.UnregisterGameTickListener(healSuppressionTickId);
                healSuppressionTickId = -1;
            }
        }

        private void OnHealSuppressionTick(float dt)
        {
            if (Api?.Side != EnumAppSide.Server) return;

            var activeBoss = FindActiveBossInArena();
            if (activeBoss == null || !activeBoss.Alive)
            {
                ClearHealSuppression();
                return;
            }

            float reduction = GetHealingReductionForBoss(activeBoss);
            ApplyHealSuppressionToPlayers(reduction);
        }

        private Entity FindActiveBossInArena()
        {
            var sapi = Api as ICoreServerAPI;
            if (sapi == null) return null;

            foreach (var entity in sapi.World.LoadedEntities.Values)
            {
                if (entity == null || !entity.Alive) continue;
                if (!entity.HasBehavior<EntityBehaviorBoss>()) continue;
                if (entity.Pos.DistanceTo(Pos.ToVec3d()) <= 80)
                    return entity;
            }
            return null;
        }

        private float GetHealingReductionForBoss(Entity boss)
        {
            var health = boss.GetBehavior<EntityBehaviorHealth>();
            if (health == null) return healingReduction;

            float hpPercent = health.Health / health.MaxHealth;
            if (hpPercent <= 0.25f) return 0.65f;
            if (hpPercent <= 0.50f) return 0.50f;
            return healingReduction;
        }

        private void ApplyHealSuppressionToPlayers(float reduction)
        {
            var sapi = Api as ICoreServerAPI;
            if (sapi == null) return;

            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.Pos == null) continue;
                if (player.Entity.Pos.Dimension != Pos.dimension) continue;
                if (player.Entity.Pos.DistanceTo(Pos.ToVec3d()) <= 80)
                {
                    player.Entity.Stats.Set("healrate", "bosshuntarenasuppression", -reduction, true);
                }
            }
        }

        private void ClearHealSuppression()
        {
            var sapi = Api as ICoreServerAPI;
            if (sapi == null) return;

            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity == null) continue;
                player.Entity.Stats.Set("healrate", "bosshuntarenasuppression", 0f, true);
            }
        }

        private void TryRegister()
        {
            if (Api?.Side != EnumAppSide.Server) return;

            try
            {
                var system = Api.ModLoader.GetModSystem<BossHuntArenaSystem>();
                system?.RegisterArena(new BlockPos(Pos.X, Pos.Y, Pos.Z, Pos.dimension), yOffset, keepInventory);
            }
            catch (Exception ex)
            {
                Api?.Logger?.Error($"[vsquest] Exception in TryRegister: {ex}");
            }
        }

        private BossHuntArenaConfigData BuildConfigData()
        {
            return new BossHuntArenaConfigData
            {
                yOffset = yOffset,
                keepInventory = keepInventory
            };
        }

        private void ApplyConfigData(BossHuntArenaConfigData data, bool markDirty)
        {
            if (data == null) return;

            yOffset = data.yOffset;
            keepInventory = data.keepInventory;

            TryRegister();

            if (markDirty)
            {
                MarkDirty(true);
            }
        }
    }
}
