using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Soul Chain: boss tethers the player with a chain (particles).
    /// While chained: player is slowed. Break by: distance OR dealing enough damage.
    /// </summary>
    public class EntityBehaviorBossSoulChain : EntityBehavior
    {
        private float chainDurationSec = 5f;
        private float slowFactor = 0.4f;
        private float breakDistance = 12f;
        private float breakDamage = 20f;
        private float cooldownSec = 15f;

        private bool chainActive;
        private double chainStartMs;
        private string chainedPlayerUid;
        private float damageDealtDuringChain;
        private double lastChainMs;
        private long tickListenerId;

        public bool IsChainActive => chainActive;

        public EntityBehaviorBossSoulChain(Entity entity) : base(entity) { }
        public override string PropertyName() => "bosssoulchain";

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);
            chainDurationSec = typeAttributes["chainDurationSec"].AsFloat(5f);
            slowFactor = typeAttributes["slowFactor"].AsFloat(0.4f);
            breakDistance = typeAttributes["breakDistance"].AsFloat(12f);
            breakDamage = typeAttributes["breakDamage"].AsFloat(20f);
            cooldownSec = typeAttributes["cooldownSec"].AsFloat(15f);

            if (entity.Api?.Side == EnumAppSide.Server)
            {
                tickListenerId = entity.World.RegisterGameTickListener(OnTick, 200);
            }
        }

        /// <summary>
        /// Chain the nearest player.
        /// </summary>
        public bool TryChain()
        {
            if (entity.Api?.Side != EnumAppSide.Server) return false;
            if (!entity.Alive || chainActive) return false;

            double nowMs = entity.World.ElapsedMilliseconds;
            if (nowMs - lastChainMs < cooldownSec * 1000) return false;

            var sapi = entity.Api as ICoreServerAPI;
            if (sapi == null) return false;

            // Find nearest player
            IServerPlayer target = null;
            double nearestDist = double.MaxValue;

            foreach (var p in sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp) continue;
                if (sp.Entity == null || !sp.Entity.Alive) continue;
                if (sp.Entity.Pos.Dimension != entity.Pos.Dimension) continue;

                double dist = sp.Entity.Pos.SquareDistanceTo(entity.Pos.XYZ);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    target = sp;
                }
            }

            if (target == null) return false;

            chainActive = true;
            chainStartMs = nowMs;
            chainedPlayerUid = target.PlayerUID;
            damageDealtDuringChain = 0;

            // Apply slow
            target.Entity.Stats.Set("walkspeed", "soulchain", -slowFactor, false);

            return true;
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (!chainActive || damage <= 0) return;

            // Track damage dealt during chain (to check break condition)
            var sourcePlayer = damageSource?.SourceEntity as EntityPlayer;
            if (sourcePlayer != null && string.Equals(sourcePlayer.PlayerUID, chainedPlayerUid, StringComparison.OrdinalIgnoreCase))
            {
                damageDealtDuringChain += damage;
            }
        }

        private void OnTick(float dt)
        {
            if (entity.Api?.Side != EnumAppSide.Server) return;
            if (!chainActive) return;

            double nowMs = entity.World.ElapsedMilliseconds;
            var sapi = entity.Api as ICoreServerAPI;

            // Check duration expired
            if (nowMs - chainStartMs >= chainDurationSec * 1000)
            {
                BreakChain(sapi);
                return;
            }

            // Check break by damage
            if (damageDealtDuringChain >= breakDamage)
            {
                BreakChain(sapi);
                return;
            }

            // Check break by distance
            if (sapi != null && !string.IsNullOrWhiteSpace(chainedPlayerUid))
            {
                var player = sapi.World.PlayerByUid(chainedPlayerUid) as IServerPlayer;
                if (player?.Entity != null && player.Entity.Alive)
                {
                    double dist = Math.Sqrt(player.Entity.Pos.SquareDistanceTo(entity.Pos.XYZ));
                    if (dist >= breakDistance)
                    {
                        BreakChain(sapi);
                        return;
                    }

                    // Spawn chain particles between boss and player
                    SpawnChainParticles(entity.Pos.XYZ, player.Entity.Pos.XYZ);
                }
                else
                {
                    BreakChain(sapi);
                }
            }
        }

        private void BreakChain(ICoreServerAPI sapi)
        {
            chainActive = false;
            lastChainMs = entity.World.ElapsedMilliseconds;

            if (sapi != null && !string.IsNullOrWhiteSpace(chainedPlayerUid))
            {
                var player = sapi.World.PlayerByUid(chainedPlayerUid) as IServerPlayer;
                player?.Entity?.Stats.Remove("walkspeed", "soulchain");
            }

            chainedPlayerUid = null;
        }

        private void SpawnChainParticles(Vec3d from, Vec3d to)
        {
            int segments = 5;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                double px = from.X + (to.X - from.X) * t;
                double py = from.Y + 1 + (to.Y + 1 - from.Y - 1) * t;
                double pz = from.Z + (to.Z - from.Z) * t;

                entity.World.SpawnParticles(new SimpleParticleProperties(
                    1, 1, ColorUtil.ToRgba(200, 180, 100, 255),
                    new Vec3d(px, py, pz), new Vec3d(px, py, pz),
                    new Vec3f(0, 0, 0), new Vec3f(0, 0, 0),
                    0.3f, 0f, 0.1f, 0.15f));
            }
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);
            if (tickListenerId != 0 && entity.World != null)
            {
                entity.World.UnregisterGameTickListener(tickListenerId);
                tickListenerId = 0;
            }

            // Clean up slow on despawn
            if (chainActive)
            {
                var sapi = entity.Api as ICoreServerAPI;
                BreakChain(sapi);
            }
        }
    }
}
