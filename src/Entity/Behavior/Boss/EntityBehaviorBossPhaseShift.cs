using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    /// <summary>
    /// Phase Shift: boss becomes invulnerable briefly and teleports to a random nearby position.
    /// Visual: dissolve + reappear particles. Configurable cooldown.
    /// </summary>
    public class EntityBehaviorBossPhaseShift : EntityBehavior
    {
        private float shiftRadius = 8f;
        private float durationMs = 1500f;
        private float cooldownMs = 10000f;
        private bool invulnDuringShift = true;

        private bool shifting;
        private double shiftStartMs;
        private Vec3d targetPos;
        private double lastShiftMs;

        public bool IsShifting => shifting;

        public EntityBehaviorBossPhaseShift(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossphaseshift";

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);
            shiftRadius = typeAttributes["shiftRadius"].AsFloat(8f);
            durationMs = typeAttributes["durationMs"].AsFloat(1500f);
            cooldownMs = typeAttributes["cooldownMs"].AsFloat(10000f);
            invulnDuringShift = typeAttributes["invulnDuringShift"].AsBool(true);
        }

        /// <summary>
        /// Trigger a phase shift.
        /// </summary>
        public bool TryShift()
        {
            if (entity.Api?.Side != EnumAppSide.Server) return false;
            if (!entity.Alive || shifting) return false;

            double nowMs = entity.World.ElapsedMilliseconds;
            if (nowMs - lastShiftMs < cooldownMs) return false;

            shifting = true;
            shiftStartMs = nowMs;

            // Pick random target position
            var rand = entity.World.Rand;
            double angle = rand.NextDouble() * Math.PI * 2;
            double dist = 3 + rand.NextDouble() * (shiftRadius - 3);
            targetPos = new Vec3d(
                entity.Pos.X + Math.Cos(angle) * dist,
                entity.Pos.Y,
                entity.Pos.Z + Math.Sin(angle) * dist
            );

            // Spawn dissolve particles at current position
            SpawnDissolveParticles(entity.Pos.XYZ);

            return true;
        }

        public void OnGameTick(float dt)
        {
            if (!shifting) return;
            if (entity.Api?.Side != EnumAppSide.Server) return;

            double nowMs = entity.World.ElapsedMilliseconds;
            if (nowMs - shiftStartMs >= durationMs)
            {
                CompleteShift();
            }
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (shifting && invulnDuringShift)
            {
                damage = 0;
            }
        }

        private void CompleteShift()
        {
            shifting = false;
            lastShiftMs = entity.World.ElapsedMilliseconds;

            if (targetPos != null)
            {
                entity.TeleportTo(targetPos);
                SpawnAppearParticles(targetPos);
            }

            targetPos = null;
        }

        private void SpawnDissolveParticles(Vec3d pos)
        {
            entity.World.SpawnParticles(new SimpleParticleProperties(
                15, 25, ColorUtil.ToRgba(150, 80, 40, 180),
                new Vec3d(pos.X - 0.5, pos.Y, pos.Z - 0.5),
                new Vec3d(pos.X + 0.5, pos.Y + 2, pos.Z + 0.5),
                new Vec3f(-0.3f, 0.2f, -0.3f), new Vec3f(0.3f, 0.5f, 0.3f),
                1.2f, 0f, 0.2f, 0.5f));
        }

        private void SpawnAppearParticles(Vec3d pos)
        {
            entity.World.SpawnParticles(new SimpleParticleProperties(
                15, 25, ColorUtil.ToRgba(180, 120, 60, 220),
                new Vec3d(pos.X - 0.5, pos.Y, pos.Z - 0.5),
                new Vec3d(pos.X + 0.5, pos.Y + 2, pos.Z + 0.5),
                new Vec3f(-0.1f, -0.3f, -0.1f), new Vec3f(0.1f, 0.1f, 0.1f),
                0.8f, 0f, 0.2f, 0.4f));
        }
    }
}
