using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VsQuest
{
    public class BlockEntityVoidRiftAnchor : BlockEntity
    {
        private const string AttrTier = "alegacyvsquest:voidrift:tier";
        private const string AttrAnchorId = "alegacyvsquest:voidrift:anchorId";
        private const string AttrYOffset = "alegacyvsquest:voidrift:yOffset";
        private const string AttrLeashRange = "alegacyvsquest:voidrift:leashRange";
        private const string AttrTrialKey = "alegacyvsquest:voidrift:trialKey";

        private const int PacketOpenGui = 3000;
        private const int PacketSave = 3001;

        private int tier;
        private string anchorId;
        private string storedTrialKey;
        private float yOffset;
        private float leashRange = 60f;

        // Particle tick
        private long particleListenerId;

        // Collapse animation state
        private bool collapsing;
        private long collapseStartMs;
        private const int CollapseDurationMs = 2500;

        private VoidRiftAnchorConfigGui dlg;

        /// <summary>
        /// The tier this anchor serves (1, 2, or 3).
        /// The system resolves which boss is active for this tier from the current rotation.
        /// </summary>
        public int Tier => tier;

        /// <summary>
        /// Resolves the current active trialKey for this anchor.
        /// If the anchor has a stored trialKey and it's in the current rotation, returns it.
        /// Otherwise returns the first active key (for backwards compatibility).
        /// </summary>
        public string GetActiveTrialKey()
        {
            if (Api?.Side != EnumAppSide.Server) return null;

            try
            {
                var system = Api.ModLoader.GetModSystem<HollowTrialSystem>();
                if (system == null) return null;

                var activeKeys = system.GetActiveTrialKeys();
                if (activeKeys == null) return null;

                // If anchor has a stored trialKey, check if it's active
                if (!string.IsNullOrWhiteSpace(storedTrialKey))
                {
                    if (activeKeys.Contains(storedTrialKey)) return storedTrialKey;
                    return null; // Our boss is not in rotation
                }

                // Fallback: return first active key
                foreach (var key in activeKeys)
                {
                    var cfg = system.FindConfig(key);
                    if (cfg != null) return key;
                }
            }
            catch { }

            return null;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                TryRegisterAnchor();
            }

            // Both sides: tick for particles
            particleListenerId = api.World.RegisterGameTickListener(OnTick, 250);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            if (particleListenerId != 0 && Api?.World != null)
            {
                Api.World.UnregisterGameTickListener(particleListenerId);
                particleListenerId = 0;
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (Api?.Side != EnumAppSide.Server) return;

            var attrs = Block?.Attributes;
            tier = attrs?["tier"].AsInt(tier) ?? tier;
            yOffset = attrs?["yOffset"].AsFloat(yOffset) ?? yOffset;

            TryRegisterAnchor();
            MarkDirty(true);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            tier = tree.GetInt(AttrTier, tier);
            anchorId = tree.GetString(AttrAnchorId, anchorId);
            storedTrialKey = tree.GetString(AttrTrialKey, storedTrialKey);
            yOffset = tree.GetFloat(AttrYOffset, yOffset);
            leashRange = tree.GetFloat(AttrLeashRange, leashRange);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt(AttrTier, tier);
            if (!string.IsNullOrWhiteSpace(anchorId)) tree.SetString(AttrAnchorId, anchorId);
            if (!string.IsNullOrWhiteSpace(storedTrialKey)) tree.SetString(AttrTrialKey, storedTrialKey);
            tree.SetFloat(AttrYOffset, yOffset);
            tree.SetFloat(AttrLeashRange, leashRange);
        }

        internal void OnInteract(IPlayer byPlayer)
        {
            if (byPlayer == null) return;

            if (Api.Side == EnumAppSide.Server)
            {
                var sp = byPlayer as IServerPlayer;
                if (sp == null || !sp.HasPrivilege(Privilege.controlserver)) return;

                var data = BuildConfigData();
                (Api as ICoreServerAPI).Network.SendBlockEntityPacket(sp, Pos, PacketOpenGui, SerializerUtil.Serialize(data));
                return;
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] bytes)
        {
            if (packetid != PacketOpenGui) return;

            var data = SerializerUtil.Deserialize<VoidRiftAnchorConfigData>(bytes);
            var capi = Api as Vintagestory.API.Client.ICoreClientAPI;

            if (dlg == null || !dlg.IsOpened())
            {
                dlg = new VoidRiftAnchorConfigGui(Pos, capi);
                dlg.Data = data;
                dlg.TryOpen();
                dlg.OnClosed += () => { dlg = null; };
            }
            else
            {
                dlg.UpdateFromServer(data);
            }
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] bytes)
        {
            var sp = fromPlayer as IServerPlayer;
            if (sp == null || !sp.HasPrivilege(Privilege.controlserver)) return;
            if (packetid != PacketSave) return;

            var data = SerializerUtil.Deserialize<VoidRiftAnchorConfigData>(bytes);
            if (data == null) return;

            tier = data.tier;
            yOffset = data.yOffset;
            leashRange = data.leashRange > 0 ? data.leashRange : 60f;

            TryRegisterAnchor();
            MarkDirty(true);

            // Send refreshed data back
            var refreshed = BuildConfigData();
            (Api as ICoreServerAPI).Network.SendBlockEntityPacket(sp, Pos, PacketOpenGui, SerializerUtil.Serialize(refreshed));
        }

        private VoidRiftAnchorConfigData BuildConfigData()
        {
            return new VoidRiftAnchorConfigData
            {
                tier = tier,
                yOffset = yOffset,
                leashRange = leashRange
            };
        }

        internal void OnRemovedServerSide()
        {
            if (Api?.Side != EnumAppSide.Server) return;

            string activeKey = GetActiveTrialKey();
            if (string.IsNullOrWhiteSpace(activeKey)) return;

            try
            {
                var system = Api.ModLoader.GetModSystem<HollowTrialSystem>();
                system?.UnsetAnchorPoint(activeKey, anchorId, new BlockPos(Pos.X, Pos.Y, Pos.Z, Pos.dimension));
            }
            catch (Exception ex)
            {
                Api?.Logger?.Error($"[VoidRiftAnchor] Exception in UnsetAnchorPoint: {ex}");
            }
        }

        /// <summary>
        /// Trigger collapse animation when boss dies. Called by HollowTrialSystem on death.
        /// </summary>
        public void TriggerCollapseAnimation()
        {
            if (Api?.Side != EnumAppSide.Server) return;
            collapsing = true;
            collapseStartMs = Api.World.ElapsedMilliseconds;
        }

        public void SetTrialKey(string newTrialKey)
        {
            storedTrialKey = newTrialKey;
            TryRegisterAnchor();
            MarkDirty(true);
        }

        private void TryRegisterAnchor()
        {
            if (Api?.Side != EnumAppSide.Server) return;
            if (tier < 1 || tier > 3) return;

            string activeKey = GetActiveTrialKey();
            if (string.IsNullOrWhiteSpace(activeKey)) return;

            if (string.IsNullOrWhiteSpace(anchorId))
            {
                anchorId = $"alegacyvsquest:voidrift:{Pos.dimension}:{Pos.X}:{Pos.Y}:{Pos.Z}";
                MarkDirty(true);
            }

            try
            {
                var system = Api.ModLoader.GetModSystem<HollowTrialSystem>();
                system?.SetAnchorPoint(activeKey, anchorId, new BlockPos(Pos.X, Pos.Y, Pos.Z, Pos.dimension), yOffset);
            }
            catch (Exception ex)
            {
                Api?.Logger?.Error($"[VoidRiftAnchor] Exception in TryRegisterAnchor: {ex}");
            }
        }

        private void OnTick(float dt)
        {
            if (Api?.World == null) return;

            // Particles only spawn server-side; they sync to clients automatically
            if (Api.Side != EnumAppSide.Server) return;

            // Determine state: alive / dead / collapsing
            bool isAlive = false;
            bool isCollapsing = collapsing;

            try
            {
                var system = Api.ModLoader.GetModSystem<HollowTrialSystem>();
                if (system != null)
                {
                    string activeKey = GetActiveTrialKey(); var entity = !string.IsNullOrWhiteSpace(activeKey) ? system.GetTrackedEntity(activeKey) : null;
                    isAlive = entity != null && entity.Alive;
                }
            }
            catch
            {
            }

            if (isCollapsing)
            {
                long elapsed = Api.World.ElapsedMilliseconds - collapseStartMs;
                if (elapsed >= CollapseDurationMs)
                {
                    collapsing = false;
                    return;
                }
                SpawnCollapseParticles(elapsed / (double)CollapseDurationMs);
                return;
            }

            if (isAlive)
            {
                SpawnAliveParticles();
            }
            else
            {
                SpawnDormantParticles();
            }
        }

        private void SpawnAliveParticles()
        {
            // Bright purple particles rising 3-4 blocks
            double cx = Pos.X + 0.5;
            double cy = Pos.Y + 1.0;
            double cz = Pos.Z + 0.5;

            Api.World.SpawnParticles(new SimpleParticleProperties(
                minQuantity: 4, maxQuantity: 8,
                color: ColorUtil.ToRgba(255, 167, 139, 250),
                minPos: new Vec3d(cx - 0.4, cy, cz - 0.4),
                maxPos: new Vec3d(cx + 0.4, cy + 0.5, cz + 0.4),
                minVelocity: new Vec3f(-0.05f, 0.4f, -0.05f),
                maxVelocity: new Vec3f(0.05f, 0.7f, 0.05f),
                lifeLength: 4.0f,
                gravityEffect: -0.01f,
                minSize: 0.2f,
                maxSize: 0.45f
            ));
        }

        private void SpawnDormantParticles()
        {
            // Dim particles rising 1-2 blocks (30% opacity)
            double cx = Pos.X + 0.5;
            double cy = Pos.Y + 1.0;
            double cz = Pos.Z + 0.5;

            Api.World.SpawnParticles(new SimpleParticleProperties(
                minQuantity: 1, maxQuantity: 3,
                color: ColorUtil.ToRgba(80, 167, 139, 250),
                minPos: new Vec3d(cx - 0.3, cy, cz - 0.3),
                maxPos: new Vec3d(cx + 0.3, cy + 0.3, cz + 0.3),
                minVelocity: new Vec3f(-0.02f, 0.15f, -0.02f),
                maxVelocity: new Vec3f(0.02f, 0.3f, 0.02f),
                lifeLength: 2.5f,
                gravityEffect: -0.005f,
                minSize: 0.15f,
                maxSize: 0.3f
            ));
        }

        private void SpawnCollapseParticles(double progress)
        {
            // Particles converge inward toward center
            double cx = Pos.X + 0.5;
            double cy = Pos.Y + 1.0;
            double cz = Pos.Z + 0.5;

            // Spawn at radius which shrinks over time
            double radius = 2.0 * (1.0 - progress);
            var rand = Api.World.Rand;

            for (int i = 0; i < 8; i++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                double x = cx + Math.Cos(angle) * radius;
                double z = cz + Math.Sin(angle) * radius;
                double y = cy + rand.NextDouble() * 2.0;

                // Velocity points toward center (negative direction from outside)
                float vx = (float)((cx - x) * 0.4);
                float vz = (float)((cz - z) * 0.4);

                Api.World.SpawnParticles(new SimpleParticleProperties(
                    minQuantity: 1, maxQuantity: 2,
                    color: ColorUtil.ToRgba(200, 167, 139, 250),
                    minPos: new Vec3d(x, y, z),
                    maxPos: new Vec3d(x + 0.05, y + 0.05, z + 0.05),
                    minVelocity: new Vec3f(vx, -0.1f, vz),
                    maxVelocity: new Vec3f(vx, 0f, vz),
                    lifeLength: 1.0f,
                    gravityEffect: 0f,
                    minSize: 0.2f,
                    maxSize: 0.4f
                ));
            }
        }
    }
}
