using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Ghost Flight behavior: entity floats above ground and periodically becomes invisible.
    /// - Hovers at configurable height above ground
    /// - Periodically fades to invisible (reduced render opacity via WatchedAttributes)
    /// - While invisible: moves faster, doesn't attack
    /// - Reappears behind the player for a surprise attack
    /// </summary>
    public class EntityBehaviorBossGhostFlight : EntityBehavior
    {
        private const string AttrInvisible = "alegacyvsquest:ghost:invisible";
        private const string AttrFloatHeight = "alegacyvsquest:ghost:floatHeight";

        private float hoverHeight = 1.5f;
        private float invisDurationSec = 3f;
        private float invisCooldownSec = 12f;
        private float invisMoveSpeed = 0.08f;
        private float reappearBehindDistance = 3f;

        private bool isInvisible;
        private double invisStartMs;
        private double lastInvisMs;
        private long tickListenerId;

        public bool IsInvisible => isInvisible;

        public EntityBehaviorBossGhostFlight(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossghostflight";

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);

            hoverHeight = typeAttributes["hoverHeight"].AsFloat(1.5f);
            invisDurationSec = typeAttributes["invisDurationSec"].AsFloat(3f);
            invisCooldownSec = typeAttributes["invisCooldownSec"].AsFloat(12f);
            invisMoveSpeed = typeAttributes["invisMoveSpeed"].AsFloat(0.08f);
            reappearBehindDistance = typeAttributes["reappearBehindDistance"].AsFloat(3f);

            // Set float height attribute for client rendering
            entity.WatchedAttributes.SetFloat(AttrFloatHeight, hoverHeight);
            entity.WatchedAttributes.MarkPathDirty(AttrFloatHeight);

            if (entity.Api?.Side == EnumAppSide.Server)
            {
                tickListenerId = entity.World.RegisterGameTickListener(OnGhostTick, 200);
            }
        }

        private void OnGhostTick(float dt)
        {
            if (entity.Api?.Side != EnumAppSide.Server) return;
            if (!entity.Alive) return;

            var sapi = entity.Api as ICoreServerAPI;
            double nowMs = entity.World.ElapsedMilliseconds;

            // Apply hover (push entity up if below hover height)
            ApplyHover();

            // Ghost particles while visible
            if (!isInvisible)
            {
                SpawnGhostTrail();
            }

            // Check if should go invisible
            if (!isInvisible && nowMs - lastInvisMs >= invisCooldownSec * 1000)
            {
                // Only go invisible if a player is nearby
                var target = FindNearestPlayer(sapi);
                if (target != null)
                {
                    GoInvisible(nowMs);
                }
            }

            // While invisible: move toward behind the player
            if (isInvisible)
            {
                var target = FindNearestPlayer(sapi);
                if (target != null)
                {
                    MoveTowardBehindPlayer(target);
                }

                // Check if should reappear
                if (nowMs - invisStartMs >= invisDurationSec * 1000)
                {
                    Reappear(sapi);
                }
            }
        }

        // Apply hover — maintain entity at hoverHeight above ground
        private void ApplyHover()
        {
            // Cancel gravity — push up to maintain hover height
            if (entity.Pos.Motion.Y < 0)
            {
                entity.Pos.Motion.Y = 0;
            }

            // Find ground level below entity
            var sapi = entity.Api as ICoreServerAPI;
            if (sapi == null) return;

            var blockAccess = sapi.World.BlockAccessor;
            int ex = (int)entity.Pos.X;
            int ey = (int)entity.Pos.Y;
            int ez = (int)entity.Pos.Z;

            // Scan down to find solid ground
            int groundY = ey;
            for (int y = ey; y > ey - 10; y--)
            {
                var block = blockAccess.GetBlock(ex, y, ez);
                if (block != null && block.SideSolid[BlockFacing.UP.Index])
                {
                    groundY = y + 1;
                    break;
                }
            }

            double targetY = groundY + hoverHeight;
            double currentY = entity.Pos.Y;
            double diff = targetY - currentY;

            if (diff > 0.1)
            {
                // Too low — push up
                entity.Pos.Motion.Y = Math.Min(0.08, diff * 0.15);
            }
            else if (diff < -0.5)
            {
                // Too high — drift down gently
                entity.Pos.Motion.Y = Math.Max(-0.03, diff * 0.05);
            }
            else
            {
                // At target — hover in place
                entity.Pos.Motion.Y = 0;
            }
        }

        private void GoInvisible(double nowMs)
        {
            isInvisible = true;
            invisStartMs = nowMs;

            entity.WatchedAttributes.SetBool(AttrInvisible, true);
            entity.WatchedAttributes.MarkPathDirty(AttrInvisible);

            // Vanish particles
            var sapi = entity.Api as ICoreServerAPI;
            if (sapi != null)
            {
                ParticleUtils.SpawnAuraSphere(sapi, entity.Pos.XYZ, 1.5f, ParticleUtils.Colors.Shadow, 20, 0.3f);
                ParticleUtils.SpawnSpiral(sapi, entity.Pos.XYZ, 1f, 2f, ParticleUtils.Colors.Void, 16, 0.2f);
            }

            // Vanish sound
            entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-active"), entity, null, true, 24);
        }

        private void Reappear(ICoreServerAPI sapi)
        {
            isInvisible = false;
            lastInvisMs = entity.World.ElapsedMilliseconds;

            // Teleport behind the nearest player on reappear
            var target = FindNearestPlayer(sapi);
            if (target != null)
            {
                double playerYaw = target.Pos.Yaw;
                double behindX = target.Pos.X + Math.Sin(playerYaw) * reappearBehindDistance;
                double behindZ = target.Pos.Z + Math.Cos(playerYaw) * reappearBehindDistance;
                entity.TeleportTo(new Vec3d(behindX, target.Pos.Y, behindZ));
            }

            entity.WatchedAttributes.SetBool(AttrInvisible, false);
            entity.WatchedAttributes.MarkPathDirty(AttrInvisible);

            // Reappear particles
            if (sapi != null)
            {
                ParticleUtils.SpawnShadowExplosion(sapi, entity.Pos.XYZ, 2f, 1);
                ParticleUtils.SpawnEntityAura(sapi, entity, ParticleUtils.Colors.Shadow, 10, 0.4f, 0.6f);
            }

            // Reappear sound
            entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-active"), entity, null, true, 24);
            entity.World.PlaySoundAt(new AssetLocation("game:sounds/creature/drifter-hurt"), entity, null, true, 16);
        }

        private void MoveTowardBehindPlayer(EntityPlayer target)
        {
            // While invisible, don't move via Motion (conflicts with AI physics).
            // Teleport happens on reappear instead.
        }

        private void SpawnGhostTrail()
        {
            var pos = entity.Pos;
            entity.World.SpawnParticles(new SimpleParticleProperties(
                1, 2, ColorUtil.ToRgba(80, 100, 50, 150),
                new Vec3d(pos.X - 0.3, pos.Y + 0.5, pos.Z - 0.3),
                new Vec3d(pos.X + 0.3, pos.Y + 1.5, pos.Z + 0.3),
                new Vec3f(0, -0.05f, 0), new Vec3f(0, 0.05f, 0),
                1.5f, -0.01f, 0.1f, 0.25f));
        }

        private EntityPlayer FindNearestPlayer(ICoreServerAPI sapi)
        {
            if (sapi == null) return null;
            EntityPlayer nearest = null;
            double nearestDist = double.MaxValue;

            foreach (var p in sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe == null || !pe.Alive) continue;
                if (pe.Pos.Dimension != entity.Pos.Dimension) continue;

                double dist = pe.Pos.SquareDistanceTo(entity.Pos.XYZ);
                if (dist < nearestDist && dist < 30 * 30)
                {
                    nearestDist = dist;
                    nearest = pe;
                }
            }

            return nearest;
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);
            if (tickListenerId != 0 && entity.World != null)
            {
                entity.World.UnregisterGameTickListener(tickListenerId);
                tickListenerId = 0;
            }
        }
    }
}
