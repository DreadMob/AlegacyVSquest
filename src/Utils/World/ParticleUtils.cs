using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Utility class for creating and spawning particle effects.
    /// Provides preset particle configurations and a builder pattern for custom effects.
    /// </summary>
    public static class ParticleUtils
    {
        // ========================================
        // PRESET COLORS
        // ========================================

        public static class Colors
        {
            public static int Fire => ColorUtil.ToRgba(255, 255, 120, 40);
            public static int FireDark => ColorUtil.ToRgba(255, 180, 60, 20);
            public static int Poison => ColorUtil.ToRgba(200, 50, 200, 50);
            public static int PoisonBright => ColorUtil.ToRgba(255, 80, 255, 60);
            public static int Ice => ColorUtil.ToRgba(220, 180, 220, 255);
            public static int IceBright => ColorUtil.ToRgba(255, 200, 240, 255);
            public static int Shadow => ColorUtil.ToRgba(200, 40, 0, 60);
            public static int ShadowDeep => ColorUtil.ToRgba(180, 20, 0, 40);
            public static int Lightning => ColorUtil.ToRgba(255, 255, 255, 180);
            public static int LightningBlue => ColorUtil.ToRgba(255, 180, 200, 255);
            public static int Holy => ColorUtil.ToRgba(255, 255, 240, 200);
            public static int HolyGold => ColorUtil.ToRgba(255, 255, 200, 80);
            public static int Blood => ColorUtil.ToRgba(220, 180, 20, 20);
            public static int BloodDark => ColorUtil.ToRgba(200, 120, 10, 10);
            public static int Arcane => ColorUtil.ToRgba(200, 120, 50, 200);
            public static int ArcaneBright => ColorUtil.ToRgba(255, 160, 80, 255);
            public static int Smoke => ColorUtil.ToRgba(140, 60, 60, 60);
            public static int SmokeDark => ColorUtil.ToRgba(140, 30, 30, 30);
            public static int Void => ColorUtil.ToRgba(180, 10, 0, 20);
            public static int Nature => ColorUtil.ToRgba(200, 60, 180, 40);
            public static int NatureBright => ColorUtil.ToRgba(255, 100, 220, 80);
            public static int Chain => ColorUtil.ToRgba(200, 150, 100, 200);
            public static int Shield => ColorUtil.ToRgba(200, 100, 150, 220);
            public static int ShieldGold => ColorUtil.ToRgba(220, 220, 180, 80);
            public static int White => ColorUtil.ToRgba(255, 255, 255, 255);
            public static int Black => ColorUtil.ToRgba(255, 10, 10, 10);
        }

        // ========================================
        // BUILDER
        // ========================================

        /// <summary>
        /// Creates a new particle effect builder.
        /// </summary>
        public static ParticleEffectBuilder Create() => new ParticleEffectBuilder();

        // ========================================
        // PRESET EFFECTS - EXPLOSIONS
        // ========================================

        /// <summary>
        /// Fire explosion with smoke and flash.
        /// </summary>
        public static void SpawnFireExplosion(ICoreServerAPI sapi, Vec3d center, float radius, int intensity = 1)
        {
            if (sapi == null) return;

            int smokeMin = Math.Max(40, (int)(radius * 30f * intensity));
            int smokeMax = Math.Max(smokeMin + 20, (int)(radius * 50f * intensity));

            // Smoke cloud
            sapi.World.SpawnParticles(new SimpleParticleProperties(
                smokeMin, smokeMax,
                Colors.SmokeDark,
                center.AddCopy(-radius * 0.5, -0.25, -radius * 0.5),
                center.AddCopy(radius * 0.5, radius * 0.4, radius * 0.5),
                new Vec3f(-0.6f, 0.05f, -0.6f),
                new Vec3f(0.6f, 0.4f, 0.6f),
                0.15f, -0.06f, 0.5f, 0.5f,
                EnumParticleModel.Quad
            ));

            // Fire flash
            int flashMin = Math.Max(10, (int)(radius * 8f * intensity));
            int flashMax = Math.Max(flashMin + 8, (int)(radius * 14f * intensity));

            sapi.World.SpawnParticles(new SimpleParticleProperties(
                flashMin, flashMax,
                Colors.Fire,
                center.AddCopy(-radius * 0.3, 0, -radius * 0.3),
                center.AddCopy(radius * 0.3, radius * 0.2, radius * 0.3),
                new Vec3f(-0.3f, 0.3f, -0.3f),
                new Vec3f(0.3f, 0.8f, 0.3f),
                0.04f, 0f, 0.15f, 0.08f,
                EnumParticleModel.Quad
            ));
        }

        /// <summary>
        /// Poison explosion with green mist.
        /// </summary>
        public static void SpawnPoisonExplosion(ICoreServerAPI sapi, Vec3d center, float radius, int intensity = 1)
        {
            if (sapi == null) return;

            int min = Math.Max(30, (int)(radius * 20f * intensity));
            int max = Math.Max(min + 15, (int)(radius * 35f * intensity));

            sapi.World.SpawnParticles(new SimpleParticleProperties(
                min, max,
                Colors.Poison,
                center.AddCopy(-radius, 0, -radius),
                center.AddCopy(radius, 1, radius),
                new Vec3f(-0.2f, 0.02f, -0.2f),
                new Vec3f(0.2f, 0.12f, 0.2f),
                2.0f, 0.05f, 0.4f, 0.6f,
                EnumParticleModel.Quad
            ));
        }

        /// <summary>
        /// Shadow/void explosion with dark particles.
        /// </summary>
        public static void SpawnShadowExplosion(ICoreServerAPI sapi, Vec3d center, float radius, int intensity = 1)
        {
            if (sapi == null) return;

            int min = Math.Max(35, (int)(radius * 25f * intensity));
            int max = Math.Max(min + 15, (int)(radius * 40f * intensity));

            sapi.World.SpawnParticles(new SimpleParticleProperties(
                min, max,
                Colors.Shadow,
                center.AddCopy(-radius * 0.6, -0.2, -radius * 0.6),
                center.AddCopy(radius * 0.6, radius * 0.5, radius * 0.6),
                new Vec3f(-0.4f, -0.1f, -0.4f),
                new Vec3f(0.4f, 0.3f, 0.4f),
                1.5f, -0.02f, 0.6f, 0.4f,
                EnumParticleModel.Quad
            ));

            // Inner void core
            sapi.World.SpawnParticles(new SimpleParticleProperties(
                min / 3, max / 3,
                Colors.Void,
                center.AddCopy(-0.3, 0, -0.3),
                center.AddCopy(0.3, 0.5, 0.3),
                new Vec3f(-0.1f, 0.1f, -0.1f),
                new Vec3f(0.1f, 0.5f, 0.1f),
                0.8f, 0f, 0.3f, 0.2f,
                EnumParticleModel.Quad
            ));
        }

        // ========================================
        // PRESET EFFECTS - AURAS
        // ========================================

        /// <summary>
        /// Spawn a ring of particles around a position (aura effect).
        /// </summary>
        public static void SpawnAuraRing(ICoreServerAPI sapi, Vec3d center, float radius, int color, int count = 12, float size = 0.3f)
        {
            if (sapi == null) return;

            for (int i = 0; i < count; i++)
            {
                double angle = (Math.PI * 2.0 / count) * i;
                double x = center.X + Math.Cos(angle) * radius;
                double z = center.Z + Math.Sin(angle) * radius;
                var pos = new Vec3d(x, center.Y, z);

                sapi.World.SpawnParticles(new SimpleParticleProperties(
                    1, 2, color,
                    pos.AddCopy(-0.1, 0, -0.1),
                    pos.AddCopy(0.1, 0.3, 0.1),
                    new Vec3f(0, 0.1f, 0),
                    new Vec3f(0, 0.3f, 0),
                    0.8f, 0f, size, size * 0.8f,
                    EnumParticleModel.Quad
                ));
            }
        }

        /// <summary>
        /// Spawn a sphere of particles around a position.
        /// </summary>
        public static void SpawnAuraSphere(ICoreServerAPI sapi, Vec3d center, float radius, int color, int count = 16, float size = 0.25f)
        {
            if (sapi == null) return;

            var rand = sapi.World.Rand;
            for (int i = 0; i < count; i++)
            {
                double theta = rand.NextDouble() * Math.PI * 2;
                double phi = rand.NextDouble() * Math.PI;
                double x = center.X + Math.Sin(phi) * Math.Cos(theta) * radius;
                double y = center.Y + Math.Cos(phi) * radius;
                double z = center.Z + Math.Sin(phi) * Math.Sin(theta) * radius;
                var pos = new Vec3d(x, y, z);

                sapi.World.SpawnParticles(new SimpleParticleProperties(
                    1, 1, color,
                    pos, pos.AddCopy(0, 0.05, 0),
                    new Vec3f(0, 0.02f, 0),
                    new Vec3f(0, 0.08f, 0),
                    0.6f, 0f, size, size * 0.6f,
                    EnumParticleModel.Quad
                ));
            }
        }

        /// <summary>
        /// Spawn a column/pillar of particles rising from a position.
        /// </summary>
        public static void SpawnPillar(ICoreServerAPI sapi, Vec3d basePos, float height, float width, int color, int count = 20)
        {
            if (sapi == null) return;

            sapi.World.SpawnParticles(new SimpleParticleProperties(
                count, count + 8, color,
                basePos.AddCopy(-width * 0.5, 0, -width * 0.5),
                basePos.AddCopy(width * 0.5, height * 0.3, width * 0.5),
                new Vec3f(-0.05f, 0.3f, -0.05f),
                new Vec3f(0.05f, 0.8f, 0.05f),
                1.2f, -0.02f, 0.3f, 0.2f,
                EnumParticleModel.Quad
            ));
        }

        // ========================================
        // PRESET EFFECTS - TRAILS & LINES
        // ========================================

        /// <summary>
        /// Spawn particles along a line between two points (chain/beam effect).
        /// </summary>
        public static void SpawnLine(ICoreServerAPI sapi, Vec3d from, Vec3d to, int color, int segments = 8, float size = 0.2f)
        {
            if (sapi == null) return;

            Vec3d dir = to.SubCopy(from);
            double length = dir.Length();
            if (length < 0.01) return;
            dir.Normalize();

            double step = length / segments;
            for (int i = 0; i <= segments; i++)
            {
                Vec3d pos = from.AddCopy(dir.X * step * i, dir.Y * step * i, dir.Z * step * i);

                sapi.World.SpawnParticles(new SimpleParticleProperties(
                    1, 2, color,
                    pos.AddCopy(-0.05, -0.05, -0.05),
                    pos.AddCopy(0.05, 0.05, 0.05),
                    new Vec3f(-0.02f, -0.02f, -0.02f),
                    new Vec3f(0.02f, 0.02f, 0.02f),
                    0.3f, 0f, size, size * 0.7f,
                    EnumParticleModel.Quad
                ));
            }
        }

        /// <summary>
        /// Spawn a spiral of particles around a position.
        /// </summary>
        public static void SpawnSpiral(ICoreServerAPI sapi, Vec3d center, float radius, float height, int color, int count = 24, float size = 0.2f)
        {
            if (sapi == null) return;

            for (int i = 0; i < count; i++)
            {
                double t = (double)i / count;
                double angle = t * Math.PI * 4; // 2 full rotations
                double y = center.Y + t * height;
                double x = center.X + Math.Cos(angle) * radius * (1 - t * 0.3); // Slightly narrowing
                double z = center.Z + Math.Sin(angle) * radius * (1 - t * 0.3);
                var pos = new Vec3d(x, y, z);

                sapi.World.SpawnParticles(new SimpleParticleProperties(
                    1, 1, color,
                    pos, pos.AddCopy(0, 0.02, 0),
                    new Vec3f(0, 0.1f, 0),
                    new Vec3f(0, 0.2f, 0),
                    0.5f, 0f, size, size * 0.5f,
                    EnumParticleModel.Quad
                ));
            }
        }

        // ========================================
        // PRESET EFFECTS - ENTITY-ATTACHED
        // ========================================

        /// <summary>
        /// Spawn particles around an entity (body glow/aura).
        /// </summary>
        public static void SpawnEntityAura(ICoreServerAPI sapi, Entity entity, int color, int count = 6, float size = 0.4f, float spread = 0.5f)
        {
            if (sapi == null || entity == null) return;

            var pos = entity.Pos.XYZ.Add(0, entity.SelectionBox.Y2 * 0.5, 0);

            sapi.World.SpawnParticles(new SimpleParticleProperties(
                count, count + 3, color,
                pos.AddCopy(-spread, -spread * 0.5, -spread),
                pos.AddCopy(spread, spread, spread),
                new Vec3f(-0.1f, 0.1f, -0.1f),
                new Vec3f(0.1f, 0.4f, 0.1f),
                0.8f, 0f, size, size * 0.6f,
                EnumParticleModel.Quad
            ));
        }

        /// <summary>
        /// Spawn impact particles at an entity's position (hit effect).
        /// </summary>
        public static void SpawnImpact(ICoreServerAPI sapi, Entity entity, int color, int count = 10, float size = 0.3f)
        {
            if (sapi == null || entity == null) return;

            var pos = entity.Pos.XYZ.Add(0, entity.SelectionBox.Y2 * 0.5, 0);

            sapi.World.SpawnParticles(new SimpleParticleProperties(
                count, count + 5, color,
                pos.AddCopy(-0.2, -0.2, -0.2),
                pos.AddCopy(0.2, 0.2, 0.2),
                new Vec3f(-0.8f, -0.3f, -0.8f),
                new Vec3f(0.8f, 0.8f, 0.8f),
                0.3f, 0.5f, size, size * 0.4f,
                EnumParticleModel.Quad
            ));
        }

        /// <summary>
        /// Spawn ground-level particles spreading outward (shockwave).
        /// </summary>
        public static void SpawnShockwave(ICoreServerAPI sapi, Vec3d center, float radius, int color, int count = 24, float size = 0.4f)
        {
            if (sapi == null) return;

            var rand = sapi.World.Rand;
            for (int i = 0; i < count; i++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                float speed = 0.3f + (float)rand.NextDouble() * 0.5f;
                float vx = (float)Math.Cos(angle) * speed;
                float vz = (float)Math.Sin(angle) * speed;

                sapi.World.SpawnParticles(new SimpleParticleProperties(
                    1, 2, color,
                    center.AddCopy(-0.2, 0, -0.2),
                    center.AddCopy(0.2, 0.3, 0.2),
                    new Vec3f(vx * 0.8f, 0.05f, vz * 0.8f),
                    new Vec3f(vx * 1.2f, 0.15f, vz * 1.2f),
                    0.6f, 0.2f, size, size * 0.5f,
                    EnumParticleModel.Quad
                ));
            }
        }

        // ========================================
        // PRESET EFFECTS - WEATHER/AMBIENT
        // ========================================

        /// <summary>
        /// Spawn falling particles (rain, ash, embers).
        /// </summary>
        public static void SpawnFalling(ICoreServerAPI sapi, Vec3d center, float radius, float height, int color, int count = 15, float size = 0.2f)
        {
            if (sapi == null) return;

            sapi.World.SpawnParticles(new SimpleParticleProperties(
                count, count + 5, color,
                center.AddCopy(-radius, height * 0.5, -radius),
                center.AddCopy(radius, height, radius),
                new Vec3f(-0.05f, -0.3f, -0.05f),
                new Vec3f(0.05f, -0.1f, 0.05f),
                1.5f, 0.3f, size, size * 0.6f,
                EnumParticleModel.Quad
            ));
        }

        /// <summary>
        /// Spawn rising embers/sparks.
        /// </summary>
        public static void SpawnEmbers(ICoreServerAPI sapi, Vec3d center, float radius, int count = 10, float size = 0.15f)
        {
            if (sapi == null) return;

            sapi.World.SpawnParticles(new SimpleParticleProperties(
                count, count + 5,
                Colors.Fire,
                center.AddCopy(-radius, 0, -radius),
                center.AddCopy(radius, 0.5, radius),
                new Vec3f(-0.1f, 0.2f, -0.1f),
                new Vec3f(0.1f, 0.6f, 0.1f),
                1.0f, -0.1f, size, size * 0.4f,
                EnumParticleModel.Quad
            ));
        }

        // ========================================
        // RECURRING PARTICLE EFFECT
        // ========================================

        /// <summary>
        /// A managed recurring particle effect that spawns particles at intervals.
        /// Similar to BossBehaviorUtils.LoopSound but for particles.
        /// </summary>
        public sealed class RecurringEffect : IDisposable
        {
            private long listenerId;
            private ICoreServerAPI sapi;
            private bool disposed;

            /// <summary>
            /// Start a recurring particle effect.
            /// </summary>
            /// <param name="sapi">Server API</param>
            /// <param name="intervalMs">Interval between spawns in milliseconds</param>
            /// <param name="spawnAction">Action to execute each interval (spawn particles)</param>
            public void Start(ICoreServerAPI sapi, int intervalMs, Action spawnAction)
            {
                Stop();
                if (sapi == null || spawnAction == null) return;

                this.sapi = sapi;
                int interval = Math.Max(100, intervalMs);

                listenerId = sapi.Event.RegisterGameTickListener(_ =>
                {
                    try
                    {
                        spawnAction();
                    }
                    catch (Exception)
                    {
                        // Particle spawn failed - entity may have been removed
                    }
                }, interval);
            }

            /// <summary>
            /// Start a recurring particle effect attached to an entity (auto-stops on death/despawn).
            /// </summary>
            public void Start(ICoreServerAPI sapi, Entity entity, int intervalMs, Action<Entity> spawnAction)
            {
                Stop();
                if (sapi == null || entity == null || spawnAction == null) return;

                this.sapi = sapi;
                int interval = Math.Max(100, intervalMs);

                listenerId = sapi.Event.RegisterGameTickListener(_ =>
                {
                    try
                    {
                        if (entity == null || !entity.Alive)
                        {
                            Stop();
                            return;
                        }
                        spawnAction(entity);
                    }
                    catch (Exception)
                    {
                        // Particle spawn failed
                    }
                }, interval);
            }

            public void Stop()
            {
                if (listenerId != 0)
                {
                    try
                    {
                        sapi?.Event?.UnregisterGameTickListener(listenerId);
                    }
                    catch (Exception) { }
                    listenerId = 0;
                }
                sapi = null;
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    Stop();
                    disposed = true;
                }
            }
        }
    }

    // ========================================
    // BUILDER CLASS
    // ========================================

    /// <summary>
    /// Fluent builder for creating custom particle effects.
    /// </summary>
    public class ParticleEffectBuilder
    {
        private int minCount = 5;
        private int maxCount = 10;
        private int color = ParticleUtils.Colors.White;
        private Vec3d minPos = new Vec3d();
        private Vec3d maxPos = new Vec3d();
        private Vec3f minVelocity = new Vec3f(0, 0, 0);
        private Vec3f maxVelocity = new Vec3f(0, 0.1f, 0);
        private float lifeLength = 1.0f;
        private float gravityEffect = 0f;
        private float minSize = 0.3f;
        private float maxSize = 0.3f;
        private EnumParticleModel model = EnumParticleModel.Quad;

        public ParticleEffectBuilder Count(int min, int max)
        {
            minCount = min;
            maxCount = max;
            return this;
        }

        public ParticleEffectBuilder Color(int rgba)
        {
            color = rgba;
            return this;
        }

        public ParticleEffectBuilder Position(Vec3d center, float spread = 0.5f)
        {
            minPos = center.AddCopy(-spread, -spread, -spread);
            maxPos = center.AddCopy(spread, spread, spread);
            return this;
        }

        public ParticleEffectBuilder Position(Vec3d min, Vec3d max)
        {
            minPos = min;
            maxPos = max;
            return this;
        }

        public ParticleEffectBuilder AtEntity(Entity entity, float spread = 0.5f)
        {
            if (entity != null)
            {
                var center = entity.Pos.XYZ.Add(0, entity.SelectionBox.Y2 * 0.5, 0);
                minPos = center.AddCopy(-spread, -spread * 0.5, -spread);
                maxPos = center.AddCopy(spread, spread, spread);
            }
            return this;
        }

        public ParticleEffectBuilder Velocity(Vec3f min, Vec3f max)
        {
            minVelocity = min;
            maxVelocity = max;
            return this;
        }

        public ParticleEffectBuilder VelocityUp(float min = 0.1f, float max = 0.4f)
        {
            minVelocity = new Vec3f(-0.05f, min, -0.05f);
            maxVelocity = new Vec3f(0.05f, max, 0.05f);
            return this;
        }

        public ParticleEffectBuilder VelocityOutward(float speed = 0.3f)
        {
            minVelocity = new Vec3f(-speed, -speed * 0.3f, -speed);
            maxVelocity = new Vec3f(speed, speed, speed);
            return this;
        }

        public ParticleEffectBuilder Life(float seconds)
        {
            lifeLength = seconds;
            return this;
        }

        public ParticleEffectBuilder Gravity(float gravity)
        {
            gravityEffect = gravity;
            return this;
        }

        public ParticleEffectBuilder Size(float min, float max)
        {
            minSize = min;
            maxSize = max;
            return this;
        }

        public ParticleEffectBuilder Size(float size)
        {
            minSize = size;
            maxSize = size;
            return this;
        }

        public ParticleEffectBuilder Model(EnumParticleModel particleModel)
        {
            model = particleModel;
            return this;
        }

        public ParticleEffectBuilder Cube()
        {
            model = EnumParticleModel.Cube;
            return this;
        }

        public ParticleEffectBuilder Quad()
        {
            model = EnumParticleModel.Quad;
            return this;
        }

        /// <summary>
        /// Build the SimpleParticleProperties object.
        /// </summary>
        public SimpleParticleProperties Build()
        {
            return new SimpleParticleProperties(
                minCount, maxCount, color,
                minPos, maxPos,
                minVelocity, maxVelocity,
                lifeLength, gravityEffect,
                minSize, maxSize,
                model
            );
        }

        /// <summary>
        /// Build and immediately spawn the particles.
        /// </summary>
        public void Spawn(ICoreServerAPI sapi)
        {
            if (sapi == null) return;
            sapi.World.SpawnParticles(Build());
        }

        /// <summary>
        /// Build and immediately spawn the particles (accepts IWorldAccessor for client-side too).
        /// </summary>
        public void Spawn(IWorldAccessor world)
        {
            if (world == null) return;
            world.SpawnParticles(Build());
        }
    }
}
