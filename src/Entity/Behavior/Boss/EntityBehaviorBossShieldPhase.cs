using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    /// <summary>
    /// Boss Shield Phase: at configured HP threshold, boss becomes invulnerable and spawns mobs.
    /// Kill all mobs to break the shield. Boss doesn't attack during shield phase.
    /// Max duration failsafe: if mobs aren't killed in time, shield breaks anyway.
    /// </summary>
    public class EntityBehaviorBossShieldPhase : EntityBehavior
    {
        private float shieldHealthThreshold = 0.5f;
        private string mobCode = "game:drifter-deep";
        private int mobCount = 4;
        private float spawnRadius = 5f;
        private float maxShieldDurationSec = 60f;

        private bool shieldActive;
        private bool hasTriggered;
        private long shieldStartMs;
        private readonly List<long> spawnedMobIds = new();

        public bool IsShieldActive => shieldActive;

        public EntityBehaviorBossShieldPhase(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossshieldphase";

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);
            shieldHealthThreshold = typeAttributes["shieldHealthThreshold"].AsFloat(0.5f);
            mobCode = typeAttributes["mobCode"].AsString("game:drifter-deep");
            mobCount = typeAttributes["mobCount"].AsInt(4);
            spawnRadius = typeAttributes["spawnRadius"].AsFloat(5f);
            maxShieldDurationSec = typeAttributes["maxShieldDurationSec"].AsFloat(60f);
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (entity?.Api?.Side != EnumAppSide.Server) return;
            if (!entity.Alive) return;

            var sapi = entity.Api as ICoreServerAPI;
            if (sapi == null) return;

            if (shieldActive)
            {
                ProcessShieldPhase(sapi, dt);
                return;
            }

            if (hasTriggered) return;

            // Check HP threshold
            var health = entity.GetBehavior<EntityBehaviorHealth>();
            if (health == null || health.MaxHealth <= 0) return;

            float frac = health.Health / health.MaxHealth;
            if (frac > shieldHealthThreshold) return;

            // Activate shield
            ActivateShield(sapi);
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            // Block all damage while shield is active
            if (shieldActive)
            {
                damage = 0;
                if (damageSource != null) damageSource.KnockbackStrength = 0;
            }
        }

        private void ActivateShield(ICoreServerAPI sapi)
        {
            shieldActive = true;
            hasTriggered = true;
            shieldStartMs = sapi.World.ElapsedMilliseconds;

            entity.WatchedAttributes.SetBool("alegacyvsquest:shieldphase", true);
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:shieldphase");

            // Stop boss AI
            entity.StopAiAndFreeze();

            // Shield activation effects
            ParticleUtils.SpawnAuraSphere(sapi, entity.Pos.XYZ, 3f, ParticleUtils.Colors.ShieldGold, 30, 0.8f);
            sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-active"),
                entity.Pos.X, entity.Pos.Y, entity.Pos.Z, null, true, 32f, 0.7f);

            // Spawn mobs
            SpawnMobWave(sapi);
        }

        private void SpawnMobWave(ICoreServerAPI sapi)
        {
            spawnedMobIds.Clear();
            var rand = sapi.World.Rand;

            var entityType = sapi.World.GetEntityType(new AssetLocation(mobCode));
            if (entityType == null)
            {
                sapi.Logger.Warning("[BossShieldPhase] Mob type not found: {0}", mobCode);
                BreakShield(sapi);
                return;
            }

            for (int i = 0; i < mobCount; i++)
            {
                double angle = (Math.PI * 2 / mobCount) * i + rand.NextDouble() * 0.5;
                double x = entity.Pos.X + Math.Cos(angle) * spawnRadius;
                double z = entity.Pos.Z + Math.Sin(angle) * spawnRadius;

                var mob = sapi.World.ClassRegistry.CreateEntity(entityType);
                if (mob == null) continue;

                mob.Pos.SetPos(new Vec3d(x, entity.Pos.Y, z));

                sapi.World.SpawnEntity(mob);
                spawnedMobIds.Add(mob.EntityId);

                // Spawn effect
                ParticleUtils.SpawnAuraSphere(sapi, new Vec3d(x, entity.Pos.Y + 1, z), 1f, ParticleUtils.Colors.Shadow, 8, 0.4f);
            }
        }

        private void ProcessShieldPhase(ICoreServerAPI sapi, float dt)
        {
            long nowMs = sapi.World.ElapsedMilliseconds;

            // Shield particles
            ParticleUtils.SpawnEntityAura(sapi, entity, ParticleUtils.Colors.ShieldGold, 6, 0.6f, 0.8f);

            // Check if all mobs dead
            bool allDead = true;
            for (int i = spawnedMobIds.Count - 1; i >= 0; i--)
            {
                var mob = sapi.World.GetEntityById(spawnedMobIds[i]);
                if (mob != null && mob.Alive)
                {
                    allDead = false;
                }
                else
                {
                    spawnedMobIds.RemoveAt(i);
                }
            }

            if (allDead)
            {
                BreakShield(sapi);
                return;
            }

            // Max duration failsafe
            if ((nowMs - shieldStartMs) / 1000.0 >= maxShieldDurationSec)
            {
                // Despawn remaining mobs
                foreach (var id in spawnedMobIds)
                {
                    var mob = sapi.World.GetEntityById(id);
                    if (mob != null && mob.Alive)
                    {
                        sapi.World.DespawnEntity(mob, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                }
                spawnedMobIds.Clear();
                BreakShield(sapi);
            }
        }

        private void BreakShield(ICoreServerAPI sapi)
        {
            shieldActive = false;

            entity.WatchedAttributes.SetBool("alegacyvsquest:shieldphase", false);
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:shieldphase");

            // Shield break effects
            ParticleUtils.SpawnShockwave(sapi, entity.Pos.XYZ, 6f, ParticleUtils.Colors.ShieldGold, 30, 0.7f);
            ParticleUtils.SpawnFireExplosion(sapi, entity.Pos.XYZ, 2f, 1);
            sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-breakdimension"),
                entity.Pos.X, entity.Pos.Y, entity.Pos.Z, null, true, 48f, 0.8f);
        }
    }
}
