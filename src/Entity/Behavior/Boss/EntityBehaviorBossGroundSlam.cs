using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Ground Slam: telegraph circle → windup → AoE damage + knockback.
    /// Leaves temporary crack particles on the ground.
    /// </summary>
    public class EntityBehaviorBossGroundSlam : EntityBehavior
    {
        private float radius = 5f;
        private float damage = 8f;
        private float windupMs = 1200f;
        private float crackDurationSec = 5f;
        private float knockbackStrength = 1.5f;
        private float cooldownMs = 8000f;

        private bool slamActive;
        private double slamStartMs;
        private Vec3d slamCenter;
        private double lastSlamMs;

        public bool IsSlamActive => slamActive;

        public EntityBehaviorBossGroundSlam(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossgroundslam";

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);
            radius = typeAttributes["radius"].AsFloat(5f);
            damage = typeAttributes["damage"].AsFloat(8f);
            windupMs = typeAttributes["windupMs"].AsFloat(1200f);
            crackDurationSec = typeAttributes["crackDurationSec"].AsFloat(5f);
            knockbackStrength = typeAttributes["knockbackStrength"].AsFloat(1.5f);
            cooldownMs = typeAttributes["cooldownMs"].AsFloat(8000f);
        }

        /// <summary>
        /// Trigger a ground slam at the boss's current position.
        /// </summary>
        public bool TrySlam()
        {
            if (entity.Api?.Side != EnumAppSide.Server) return false;
            if (!entity.Alive || slamActive) return false;

            double nowMs = entity.World.ElapsedMilliseconds;
            if (nowMs - lastSlamMs < cooldownMs) return false;

            slamActive = true;
            slamStartMs = nowMs;
            slamCenter = entity.Pos.XYZ.Clone();

            // Spawn telegraph particles
            SpawnTelegraphParticles();
            return true;
        }

        public void OnGameTick(float dt)
        {
            if (entity.Api?.Side != EnumAppSide.Server) return;
            if (!slamActive) return;

            double nowMs = entity.World.ElapsedMilliseconds;
            if (nowMs - slamStartMs >= windupMs)
            {
                ExecuteSlam();
            }
        }

        private void ExecuteSlam()
        {
            slamActive = false;
            lastSlamMs = entity.World.ElapsedMilliseconds;

            if (slamCenter == null) return;

            // Damage + knockback all players in radius
            var sapi = entity.Api as ICoreServerAPI;
            if (sapi == null) return;

            foreach (var p in sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe == null || !pe.Alive) continue;
                if (pe.Pos.Dimension != entity.Pos.Dimension) continue;

                double dx = pe.Pos.X - slamCenter.X;
                double dz = pe.Pos.Z - slamCenter.Z;
                double distSq = dx * dx + dz * dz;

                if (distSq <= radius * radius)
                {
                    // Deal damage
                    pe.ReceiveDamage(new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.BluntAttack
                    }, damage);

                    // Knockback
                    double dist = Math.Sqrt(distSq);
                    if (dist > 0.1)
                    {
                        float kbX = (float)(dx / dist) * knockbackStrength;
                        float kbZ = (float)(dz / dist) * knockbackStrength;
                        pe.SidedPos.Motion.Add(kbX, 0.3f * knockbackStrength, kbZ);
                    }
                }
            }

            // Spawn crack/impact particles
            SpawnImpactParticles();
        }

        private void SpawnTelegraphParticles()
        {
            if (slamCenter == null) return;
            var rand = entity.World.Rand;

            for (int i = 0; i < (int)(radius * 4); i++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                double r = Math.Sqrt(rand.NextDouble()) * radius;
                double px = slamCenter.X + Math.Cos(angle) * r;
                double pz = slamCenter.Z + Math.Sin(angle) * r;

                entity.World.SpawnParticles(new SimpleParticleProperties(
                    1, 1, ColorUtil.ToRgba(160, 200, 100, 50),
                    new Vec3d(px, slamCenter.Y + 0.1, pz), new Vec3d(px, slamCenter.Y + 0.1, pz),
                    new Vec3f(0, 0.02f, 0), new Vec3f(0, 0.05f, 0),
                    0.8f, 0f, 0.2f, 0.4f));
            }
        }

        private void SpawnImpactParticles()
        {
            if (slamCenter == null) return;
            var rand = entity.World.Rand;

            for (int i = 0; i < (int)(radius * 6); i++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                double r = Math.Sqrt(rand.NextDouble()) * radius;
                double px = slamCenter.X + Math.Cos(angle) * r;
                double pz = slamCenter.Z + Math.Sin(angle) * r;

                entity.World.SpawnParticles(new SimpleParticleProperties(
                    1, 2, ColorUtil.ToRgba(200, 80, 60, 40),
                    new Vec3d(px, slamCenter.Y, pz), new Vec3d(px, slamCenter.Y + 0.5, pz),
                    new Vec3f(0, 0.1f, 0), new Vec3f(0, 0.4f, 0),
                    crackDurationSec, 0.05f, 0.1f, 0.25f));
            }
        }
    }
}
