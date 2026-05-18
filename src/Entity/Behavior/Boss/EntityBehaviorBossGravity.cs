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
    /// Boss Gravity: creates a gravity sphere that pulls players toward its center.
    /// Large visible particle sphere. Players reaching center take damage + brief stun.
    /// </summary>
    public class EntityBehaviorBossGravity : BossAbilityBase
    {
        private const string LastGravityKey = "alegacyvsquest:bossgravity:lastMs";
        protected override string CooldownKey => LastGravityKey;

        private class Stage : BossAbilityStage
        {
            public float pullRadius;
            public float pullDurationSec;
            public float pullStrength;
            public float centerDamage;
            public string sound;
            public float soundRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                pullRadius = json["pullRadius"].AsFloat(8f);
                pullDurationSec = json["pullDurationSec"].AsFloat(5f);
                pullStrength = json["pullStrength"].AsFloat(0.12f);
                centerDamage = json["centerDamage"].AsFloat(15f);
                sound = json["sound"].AsString("effect/translocate-active");
                soundRange = json["soundRange"].AsFloat(32f);
            }
        }

        private List<Stage> stages = new();
        private bool gravityActive;
        private Vec3d gravityCenter;
        private long gravityStartMs;
        private int activeStageIndex;
        private readonly HashSet<string> hitPlayers = new(StringComparer.OrdinalIgnoreCase);

        public EntityBehaviorBossGravity(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossgravity";

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

            if (gravityActive)
            {
                if (activeStageIndex < 0 || activeStageIndex >= stages.Count)
                {
                    gravityActive = false;
                    return;
                }

                var stage = stages[activeStageIndex];
                double elapsed = (nowMs - gravityStartMs) / 1000.0;

                if (elapsed >= stage.pullDurationSec)
                {
                    gravityActive = false;
                    gravityCenter = null;
                    hitPlayers.Clear();
                    return;
                }

                // Large sphere particles at center
                ParticleUtils.SpawnAuraSphere(Sapi, gravityCenter, 2f, ParticleUtils.Colors.Void, 12, 0.8f);
                // Inward-flowing particles at pull radius
                SpawnInflowParticles(stage.pullRadius);

                // Periodic pull sound while active (every 1.5 sec)
                if ((int)(elapsed * 1000) % 1500 < (int)(dt * 1000) + 50)
                {
                    entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-idle"), gravityCenter.X, gravityCenter.Y, gravityCenter.Z, null, true, 56, 0.5f);
                }

                // Pull players toward center
                foreach (var p in Sapi.World.AllOnlinePlayers)
                {
                    if (p is not IServerPlayer sp) continue;
                    var pe = sp.Entity;
                    if (pe == null || !pe.Alive) continue;
                    if (pe.Pos.Dimension != entity.Pos.Dimension) continue;

                    double dx = pe.Pos.X - gravityCenter.X;
                    double dz = pe.Pos.Z - gravityCenter.Z;
                    double distSq = dx * dx + dz * dz;

                    if (distSq > stage.pullRadius * stage.pullRadius) continue;

                    double dist = Math.Sqrt(distSq);

                    // Center hit (within 1 block)
                    if (dist < 1.0 && !hitPlayers.Contains(sp.PlayerUID))
                    {
                        hitPlayers.Add(sp.PlayerUID);
                        pe.ReceiveDamage(new DamageSource
                        {
                            Source = EnumDamageSource.Entity,
                            SourceEntity = entity,
                            Type = EnumDamageType.Injury,
                            DamageTier = 3
                        }, ApplyDamageMultiplier(stage.centerDamage));

                        // Brief stun
                        pe.Stats.Set("walkspeed", "gravitystun", -0.95f, false);
                        RegisterCallbackTracked(_ => { pe?.Stats?.Remove("walkspeed", "gravitystun"); }, 1000);

                        ParticleUtils.SpawnImpact(Sapi, pe, ParticleUtils.Colors.Void, 15, 0.5f);
                        entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/reverbhit"), pe.Pos.X, pe.Pos.Y, pe.Pos.Z, null, true, 56, 0.6f);
                        continue;
                    }

                    // Pull force (stronger closer to edge, weaker near center)
                    if (dist > 1.0)
                    {
                        double pullForce = stage.pullStrength * (dist / stage.pullRadius);
                        double dirX = -(dx / dist) * pullForce;
                        double dirZ = -(dz / dist) * pullForce;
                        pe.Pos.Motion.Add(dirX, 0, dirZ);
                    }
                }

                return;
            }

            // Try to activate gravity
            if (!entity.TryGetHealthFraction(out float frac)) return;
            var (stageObj, stageIndex) = FindStageForHealth(frac);
            if (stageObj is not Stage stg) return;
            if (!IsCooldownReady(stageObj)) return;
            if (!TargetingSystem.TryFindTarget(stg.maxTargetRange, 0, out EntityPlayer target, out _)) return;

            // Activate at target position
            MarkCooldownStart();
            gravityActive = true;
            gravityCenter = target.Pos.XYZ.Clone();
            gravityStartMs = nowMs;
            activeStageIndex = stageIndex;
            hitPlayers.Clear();

            // Activation effects
            ParticleUtils.SpawnAuraSphere(Sapi, gravityCenter, 3f, ParticleUtils.Colors.Void, 30, 1.0f);
            TryPlaySound(stg.sound, stg.soundRange);
        }

        private void SpawnInflowParticles(float radius)
        {
            if (gravityCenter == null || Sapi == null) return;

            var rand = Sapi.World.Rand;
            for (int i = 0; i < 8; i++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                double x = gravityCenter.X + Math.Cos(angle) * radius;
                double z = gravityCenter.Z + Math.Sin(angle) * radius;
                double y = gravityCenter.Y + rand.NextDouble() * 1.5;

                float vx = (float)((gravityCenter.X - x) * 0.3);
                float vz = (float)((gravityCenter.Z - z) * 0.3);

                Sapi.World.SpawnParticles(new SimpleParticleProperties(
                    1, 1, ParticleUtils.Colors.Void,
                    new Vec3d(x, y, z), new Vec3d(x + 0.1, y + 0.1, z + 0.1),
                    new Vec3f(vx, 0, vz), new Vec3f(vx * 1.2f, 0.05f, vz * 1.2f),
                    1.2f, 0f, 0.3f, 0.6f
                ));
            }
        }

        protected override void StopAbility()
        {
            gravityActive = false;
            gravityCenter = null;
            hitPlayers.Clear();
        }
    }
}
