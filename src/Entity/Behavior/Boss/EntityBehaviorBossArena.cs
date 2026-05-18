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
    /// Boss Arena: creates a ring boundary around the boss's activation position.
    /// Players who cross the boundary take damage. Arena does not move with the boss.
    /// </summary>
    public class EntityBehaviorBossArena : BossAbilityBase
    {
        private const string LastArenaKey = "alegacyvsquest:bossarena:lastMs";
        protected override string CooldownKey => LastArenaKey;

        private class Stage : BossAbilityStage
        {
            public float arenaRadius;
            public float arenaDurationSec;
            public float boundaryDps;
            public string sound;
            public float soundRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                arenaRadius = json["arenaRadius"].AsFloat(12f);
                arenaDurationSec = json["arenaDurationSec"].AsFloat(20f);
                boundaryDps = json["boundaryDps"].AsFloat(8f);
                sound = json["sound"].AsString("effect/translocate-active");
                soundRange = json["soundRange"].AsFloat(32f);
            }
        }

        private List<Stage> stages = new();
        private bool arenaActive;
        private Vec3d arenaCenter;
        private long arenaStartMs;
        private int activeStageIndex;

        public EntityBehaviorBossArena(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossarena";

        protected override bool UsePeriodicTick() => true;

        protected override void InitializeStages(JsonObject attributes) { stages = ParseStages<Stage>(attributes); }
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target) { }

        protected override void OnPeriodicTick(float dt)
        {
            if (Sapi == null || !entity.Alive) return;
            if (IsBossClone) return;
            if (stages.Count == 0) return;

            long nowMs = Sapi.World.ElapsedMilliseconds;

            // If arena active — process boundary damage + particles
            if (arenaActive)
            {
                if (activeStageIndex < 0 || activeStageIndex >= stages.Count)
                {
                    arenaActive = false;
                    return;
                }

                var stage = stages[activeStageIndex];
                double elapsed = (nowMs - arenaStartMs) / 1000.0;

                // Expired
                if (elapsed >= stage.arenaDurationSec)
                {
                    arenaActive = false;
                    arenaCenter = null;
                    return;
                }

                // Periodic hum sound while arena is active (every 3 sec)
                if (arenaCenter != null && (int)(elapsed * 1000) % 3000 < (int)(dt * 1000) + 50)
                {
                    entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-idle"), arenaCenter.X, arenaCenter.Y, arenaCenter.Z, null, true, 48, 0.3f);
                }

                // Spawn ring particles
                SpawnRingParticles(stage.arenaRadius);

                // Damage players outside boundary
                float dmg = stage.boundaryDps * dt;
                if (dmg > 0)
                {
                    foreach (var p in Sapi.World.AllOnlinePlayers)
                    {
                        if (p is not IServerPlayer sp) continue;
                        var pe = sp.Entity;
                        if (pe == null || !pe.Alive) continue;
                        if (pe.Pos.Dimension != entity.Pos.Dimension) continue;

                        double dx = pe.Pos.X - arenaCenter.X;
                        double dz = pe.Pos.Z - arenaCenter.Z;
                        double distSq = dx * dx + dz * dz;
                        double tolerance = stage.arenaRadius - 1.0;

                        if (distSq > tolerance * tolerance)
                        {
                            pe.ReceiveDamage(new DamageSource
                            {
                                Source = EnumDamageSource.Entity,
                                SourceEntity = entity,
                                Type = EnumDamageType.Fire
                            }, dmg);

                            entity.World.PlaySoundAt(new AssetLocation("game:sounds/player/projectilehit"), pe.Pos.X, pe.Pos.Y, pe.Pos.Z, null, true, 40, 0.4f);

                            // Push player back toward center
                            double dist = Math.Sqrt(distSq);
                            if (dist > 0.1)
                            {
                                double pushX = -(dx / dist) * 0.15;
                                double pushZ = -(dz / dist) * 0.15;
                                pe.Pos.Motion.Add(pushX, 0, pushZ);
                            }
                        }
                    }
                }

                return;
            }

            // Try to activate arena
            if (!entity.TryGetHealthFraction(out float frac)) return;
            var (stageObj, stageIndex) = FindStageForHealth(frac);
            if (stageObj is not Stage stg) return;
            if (!IsCooldownReady(stageObj)) return;

            // Need a target nearby to activate
            if (!TargetingSystem.TryFindTarget(stg.maxTargetRange, 0, out _, out _)) return;

            // Activate arena
            MarkCooldownStart();
            arenaActive = true;
            arenaCenter = entity.Pos.XYZ.Clone();
            arenaStartMs = nowMs;
            activeStageIndex = stageIndex;

            // Activation effects
            ParticleUtils.SpawnAuraRing(Sapi, arenaCenter, stg.arenaRadius, ParticleUtils.Colors.Fire, 40, 0.6f);
            TryPlaySound(stg.sound, stg.soundRange);
        }

        private void SpawnRingParticles(float radius)
        {
            if (arenaCenter == null || Sapi == null) return;

            var rand = Sapi.World.Rand;
            int count = (int)(radius * 3);

            for (int i = 0; i < count; i++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                double x = arenaCenter.X + Math.Cos(angle) * radius;
                double z = arenaCenter.Z + Math.Sin(angle) * radius;
                double y = arenaCenter.Y + rand.NextDouble() * 2.0;

                Sapi.World.SpawnParticles(new SimpleParticleProperties(
                    1, 1, ParticleUtils.Colors.Fire,
                    new Vec3d(x, y, z),
                    new Vec3d(x + 0.1, y + 0.3, z + 0.1),
                    new Vec3f(0, 0.1f, 0), new Vec3f(0, 0.2f, 0),
                    0.8f, -0.01f, 0.3f, 0.5f
                ));
            }
        }

        protected override void StopAbility()
        {
            arenaActive = false;
            arenaCenter = null;
        }
    }
}
