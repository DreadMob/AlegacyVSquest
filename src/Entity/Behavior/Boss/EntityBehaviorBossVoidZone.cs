using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Void Zone: places an expanding damage zone on the ground.
    /// Grows from 0 to maxRadius over growDuration. Deals DPS while standing in it.
    /// Disappears after lifetime.
    /// </summary>
    public class EntityBehaviorBossVoidZone : EntityBehavior
    {
        private float maxRadius = 6f;
        private float growDurationSec = 3f;
        private float dpsInZone = 4f;
        private float lifetimeSec = 8f;
        private float cooldownSec = 12f;
        private int maxActiveZones = 3;

        private readonly List<VoidZoneInstance> activeZones = new();
        private double lastZonePlacedMs;
        private long tickListenerId;

        public EntityBehaviorBossVoidZone(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossvoidzone";

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);
            maxRadius = typeAttributes["maxRadius"].AsFloat(6f);
            growDurationSec = typeAttributes["growDurationSec"].AsFloat(3f);
            dpsInZone = typeAttributes["dpsInZone"].AsFloat(4f);
            lifetimeSec = typeAttributes["lifetimeSec"].AsFloat(8f);
            cooldownSec = typeAttributes["cooldownSec"].AsFloat(12f);
            maxActiveZones = typeAttributes["maxActiveZones"].AsInt(3);

            if (entity.Api?.Side == EnumAppSide.Server)
            {
                tickListenerId = entity.World.RegisterGameTickListener(OnTick, 500);
            }
        }

        /// <summary>
        /// Place a void zone at the specified position.
        /// </summary>
        public bool TryPlaceZone(Vec3d position)
        {
            if (entity.Api?.Side != EnumAppSide.Server) return false;
            if (!entity.Alive) return false;

            double nowMs = entity.World.ElapsedMilliseconds;
            if (nowMs - lastZonePlacedMs < cooldownSec * 1000) return false;
            if (activeZones.Count >= maxActiveZones) return false;

            activeZones.Add(new VoidZoneInstance
            {
                center = position.Clone(),
                spawnedAtMs = nowMs,
                dim = entity.Pos.Dimension
            });

            lastZonePlacedMs = nowMs;
            return true;
        }

        /// <summary>
        /// Place a void zone at the player's current position.
        /// </summary>
        public bool TryPlaceZoneAtTarget()
        {
            var sapi = entity.Api as ICoreServerAPI;
            if (sapi == null) return false;

            // Find nearest player
            Entity target = FindNearestPlayer(sapi);
            if (target == null) return false;

            return TryPlaceZone(target.Pos.XYZ);
        }

        private void OnTick(float dt)
        {
            if (entity.Api?.Side != EnumAppSide.Server) return;

            double nowMs = entity.World.ElapsedMilliseconds;
            var sapi = entity.Api as ICoreServerAPI;

            // Process active zones
            for (int i = activeZones.Count - 1; i >= 0; i--)
            {
                var zone = activeZones[i];
                double ageMs = nowMs - zone.spawnedAtMs;
                double ageSec = ageMs / 1000.0;

                // Remove expired zones
                if (ageSec >= lifetimeSec)
                {
                    activeZones.RemoveAt(i);
                    continue;
                }

                // Calculate current radius (grows over time)
                float currentRadius = (float)Math.Min(maxRadius, maxRadius * (ageSec / growDurationSec));

                // Spawn zone particles
                SpawnZoneParticles(zone.center, currentRadius);

                // Damage players in zone
                if (sapi != null)
                {
                    DamagePlayersInZone(sapi, zone, currentRadius, dt);
                }
            }
        }

        private void DamagePlayersInZone(ICoreServerAPI sapi, VoidZoneInstance zone, float currentRadius, float dt)
        {
            float dmg = dpsInZone * dt;
            if (dmg <= 0) return;

            foreach (var p in sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe == null || !pe.Alive) continue;
                if (pe.Pos.Dimension != zone.dim) continue;

                double dx = pe.Pos.X - zone.center.X;
                double dz = pe.Pos.Z - zone.center.Z;

                if (dx * dx + dz * dz <= currentRadius * currentRadius)
                {
                    pe.ReceiveDamage(new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.Poison
                    }, dmg);
                }
            }
        }

        private void SpawnZoneParticles(Vec3d center, float currentRadius)
        {
            if (center == null || currentRadius < 0.5f) return;
            var rand = entity.World.Rand;

            int count = (int)(currentRadius * 2);
            for (int i = 0; i < count; i++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                double r = rand.NextDouble() * currentRadius;
                double px = center.X + Math.Cos(angle) * r;
                double pz = center.Z + Math.Sin(angle) * r;

                entity.World.SpawnParticles(new SimpleParticleProperties(
                    1, 1, ColorUtil.ToRgba(140, 100, 0, 180),
                    new Vec3d(px, center.Y + 0.1, pz), new Vec3d(px, center.Y + 0.3, pz),
                    new Vec3f(0, 0.05f, 0), new Vec3f(0, 0.15f, 0),
                    0.5f, 0f, 0.1f, 0.2f));
            }
        }

        private Entity FindNearestPlayer(ICoreServerAPI sapi)
        {
            Entity nearest = null;
            double nearestDist = double.MaxValue;

            foreach (var p in sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe == null || !pe.Alive) continue;
                if (pe.Pos.Dimension != entity.Pos.Dimension) continue;

                double dist = pe.Pos.SquareDistanceTo(entity.Pos.XYZ);
                if (dist < nearestDist)
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
            activeZones.Clear();
        }

        private class VoidZoneInstance
        {
            public Vec3d center;
            public double spawnedAtMs;
            public int dim;
        }
    }
}
