using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Retaliation: boss stores incoming damage and releases it back as a burst after a threshold.
    /// Visual: boss glows brighter as it absorbs damage, then explodes in AoE.
    /// Teaches players to manage DPS windows — don't over-commit.
    /// </summary>
    public class EntityBehaviorBossRetaliation : EntityBehavior
    {
        private float damageThreshold = 30f;
        private float retaliationMultiplier = 0.6f; // returns 60% of absorbed damage
        private float retaliationRadius = 6f;
        private float windowDurationSec = 4f; // absorption window
        private float cooldownSec = 15f;
        private bool autoActivate = true; // activates automatically on damage

        private bool absorbing;
        private double absorbStartMs;
        private float absorbedDamage;
        private double lastRetaliationMs;

        public bool IsAbsorbing => absorbing;
        public float AbsorbProgress => absorbing ? Math.Min(1f, absorbedDamage / damageThreshold) : 0f;

        public EntityBehaviorBossRetaliation(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossretaliation";

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);
            damageThreshold = typeAttributes["damageThreshold"].AsFloat(30f);
            retaliationMultiplier = typeAttributes["retaliationMultiplier"].AsFloat(0.6f);
            retaliationRadius = typeAttributes["retaliationRadius"].AsFloat(6f);
            windowDurationSec = typeAttributes["windowDurationSec"].AsFloat(4f);
            cooldownSec = typeAttributes["cooldownSec"].AsFloat(15f);
            autoActivate = typeAttributes["autoActivate"].AsBool(true);
        }

        /// <summary>
        /// Manually start absorption window.
        /// </summary>
        public bool TryActivate()
        {
            if (entity.Api?.Side != EnumAppSide.Server) return false;
            if (!entity.Alive || absorbing) return false;

            double nowMs = entity.World.ElapsedMilliseconds;
            if (nowMs - lastRetaliationMs < cooldownSec * 1000) return false;

            StartAbsorbing();
            return true;
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (entity.Api?.Side != EnumAppSide.Server) return;
            if (damage <= 0) return;

            // Auto-activate on first damage after cooldown
            if (!absorbing && autoActivate)
            {
                double nowMs = entity.World.ElapsedMilliseconds;
                if (nowMs - lastRetaliationMs >= cooldownSec * 1000)
                {
                    StartAbsorbing();
                }
            }

            if (absorbing)
            {
                absorbedDamage += damage;
                SpawnAbsorbParticles();

                // Check threshold
                if (absorbedDamage >= damageThreshold)
                {
                    Retaliate();
                }
            }
        }

        public void OnGameTick(float dt)
        {
            if (entity.Api?.Side != EnumAppSide.Server) return;
            if (!absorbing) return;

            double nowMs = entity.World.ElapsedMilliseconds;

            // Window expired without reaching threshold — release what we have
            if (nowMs - absorbStartMs >= windowDurationSec * 1000)
            {
                if (absorbedDamage > 0)
                {
                    Retaliate();
                }
                else
                {
                    absorbing = false;
                }
            }
        }

        private void StartAbsorbing()
        {
            absorbing = true;
            absorbStartMs = entity.World.ElapsedMilliseconds;
            absorbedDamage = 0;

            entity.WatchedAttributes.SetBool("alegacyvsquest:retaliation:absorbing", true);
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:retaliation:absorbing");
        }

        private void Retaliate()
        {
            absorbing = false;
            lastRetaliationMs = entity.World.ElapsedMilliseconds;

            entity.WatchedAttributes.SetBool("alegacyvsquest:retaliation:absorbing", false);
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:retaliation:absorbing");

            float retDamage = absorbedDamage * retaliationMultiplier;
            absorbedDamage = 0;

            if (retDamage < 1f) return;

            // AoE damage to all players in radius
            var sapi = entity.Api as ICoreServerAPI;
            if (sapi == null) return;

            foreach (var p in sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe == null || !pe.Alive) continue;
                if (pe.Pos.Dimension != entity.Pos.Dimension) continue;

                double dist = Math.Sqrt(pe.Pos.SquareDistanceTo(entity.Pos.XYZ));
                if (dist <= retaliationRadius)
                {
                    // Damage falls off with distance
                    float falloff = 1f - (float)(dist / retaliationRadius) * 0.5f;
                    pe.ReceiveDamage(new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.Injury
                    }, retDamage * falloff);
                }
            }

            // Explosion particles
            SpawnRetaliationParticles();
        }

        private void SpawnAbsorbParticles()
        {
            var pos = entity.Pos;
            float intensity = Math.Min(1f, absorbedDamage / damageThreshold);

            entity.World.SpawnParticles(new SimpleParticleProperties(
                2, 4, ColorUtil.ToRgba((int)(100 + intensity * 155), 200, 50 + (int)(intensity * 150), 50),
                new Vec3d(pos.X - 1, pos.Y + 0.5, pos.Z - 1),
                new Vec3d(pos.X + 1, pos.Y + 1.5, pos.Z + 1),
                new Vec3f(0, -0.1f, 0), new Vec3f(0, 0.1f, 0),
                0.4f, 0f, 0.1f, 0.2f + intensity * 0.2f));
        }

        private void SpawnRetaliationParticles()
        {
            var pos = entity.Pos;
            entity.World.SpawnParticles(new SimpleParticleProperties(
                30, 50, ColorUtil.ToRgba(230, 255, 100, 50),
                new Vec3d(pos.X - 1, pos.Y, pos.Z - 1),
                new Vec3d(pos.X + 1, pos.Y + 2, pos.Z + 1),
                new Vec3f(-0.5f, 0.2f, -0.5f), new Vec3f(0.5f, 0.8f, 0.5f),
                1.2f, 0.02f, 0.2f, 0.6f));
        }
    }
}
