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
    /// Ground Slam: telegraph particles → windup delay → AoE damage + knockback in radius.
    /// Uses RegisterCallbackTracked for delayed execution after windup.
    /// </summary>
    public class EntityBehaviorBossGroundSlam : BossAbilityBase
    {
        private const string LastSlamKey = "alegacyvsquest:bossgroundslam:lastMs";
        protected override string CooldownKey => LastSlamKey;

        private class Stage : BossAbilityStage
        {
            public float radius;
            public float damage;
            public float windupMs;
            public float knockbackStrength;
            public string sound;
            public float soundRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                radius = json["radius"].AsFloat(5f);
                damage = json["damage"].AsFloat(8f);
                windupMs = json["windupMs"].AsFloat(1200f);
                knockbackStrength = json["knockbackStrength"].AsFloat(1.5f);
                sound = json["sound"].AsString("environment/largerock1");
                soundRange = json["soundRange"].AsFloat(32f);
            }
        }

        private List<Stage> stages = new();

        public EntityBehaviorBossGroundSlam(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossgroundslam";

        protected override void InitializeStages(JsonObject attributes) { stages = ParseStages<Stage>(attributes); }
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;
            MarkCooldownStart();

            Vec3d slamCenter = entity.Pos.XYZ.Clone();

            // Telegraph particles during windup
            SpawnTelegraphParticles(slamCenter, stage.radius);

            // Delayed execution after windup
            RegisterCallbackTracked(_ =>
            {
                if (entity == null || !entity.Alive) return;
                ExecuteSlam(stage, slamCenter);
            }, (int)stage.windupMs);
        }

        private void ExecuteSlam(Stage stage, Vec3d slamCenter)
        {
            if (Sapi == null) return;

            float finalDamage = ApplyDamageMultiplier(stage.damage);

            // Damage + knockback all players in radius
            foreach (var p in Sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe == null || !pe.Alive) continue;
                if (pe.Pos.Dimension != entity.Pos.Dimension) continue;

                double dx = pe.Pos.X - slamCenter.X;
                double dz = pe.Pos.Z - slamCenter.Z;
                double distSq = dx * dx + dz * dz;

                if (distSq <= stage.radius * stage.radius)
                {
                    pe.ReceiveDamage(new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.BluntAttack
                    }, finalDamage);

                    // Knockback
                    double dist = Math.Sqrt(distSq);
                    if (dist > 0.1)
                    {
                        float kbX = (float)(dx / dist) * stage.knockbackStrength;
                        float kbZ = (float)(dz / dist) * stage.knockbackStrength;
                        pe.Pos.Motion.Add(kbX, 0.3f * stage.knockbackStrength, kbZ);
                    }
                }
            }

            // Shockwave particles on impact
            ParticleUtils.SpawnShockwave(Sapi, slamCenter, stage.radius, ParticleUtils.Colors.Smoke, (int)(stage.radius * 6), 0.5f);

            // Secondary shockwave for visual impact
            ParticleUtils.SpawnShockwave(Sapi, slamCenter, stage.radius * 0.5f, ParticleUtils.Colors.SmokeDark, (int)(stage.radius * 3), 0.3f);

            // Sound
            TryPlaySound(stage.sound, stage.soundRange);

            SetAbilityActive(false);
        }

        private void SpawnTelegraphParticles(Vec3d center, float radius)
        {
            if (Sapi == null || center == null) return;

            ParticleUtils.SpawnAuraRing(Sapi, center, radius, ParticleUtils.Colors.Smoke, (int)(radius * 4), 0.25f);
        }

        protected override void StopAbility() { }
    }
}
