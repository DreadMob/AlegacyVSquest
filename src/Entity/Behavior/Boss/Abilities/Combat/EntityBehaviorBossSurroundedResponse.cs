using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossSurroundedResponse : BossAbilityBase
    {
        protected override string CooldownKey => "alegacyvsquest:bosssurroundedresponse:lastUseMs";
        protected override bool UseHealthBasedStages() => false;
        protected override bool RequiresTarget() => false;

        private class Stage : BossAbilityStage
        {
            public int surroundedThreshold;
            public int shockwaveCooldownMs;
            public int slamCooldownMs;
            public float knockbackRange;
            public float slamDamage;
            public int stunMs;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                surroundedThreshold = json["surroundedThreshold"].AsInt(3);
                shockwaveCooldownMs = json["shockwaveCooldownSeconds"].AsInt(10) * 1000;
                slamCooldownMs = json["slamCooldownSeconds"].AsInt(8) * 1000;
                knockbackRange = json["knockbackRange"].AsFloat(8f);
                slamDamage = json["slamDamage"].AsFloat(22f);
                stunMs = json["stunMs"].AsInt(1000);
            }
        }

        private List<Stage> stages = new List<Stage>();
        private long lastShockwaveMs;
        private long lastSlamMs;

        public EntityBehaviorBossSurroundedResponse(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosssurroundedresponse";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        protected override int GetStageCount() => stages.Count;

        protected override object GetStage(int index) => stages[index];

        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;

        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;

        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override bool ShouldCheckAbility()
        {
            if (stages.Count == 0) return false;
            var stage = stages[0];
            return !IsAbilityActive && CountNearbyPlayers(stage.maxTargetRange) >= stage.surroundedThreshold;
        }

        protected override void StopAbility()
        {
        }

        protected override bool OnAbilityTick(float dt) => false;

        private int CountNearbyPlayers(float range)
        {
            int count = 0;
            var players = Sapi.World.AllOnlinePlayers;
            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                var plrEntity = player.Entity;
                if (plrEntity?.Pos == null) continue;
                if (plrEntity.Pos.Dimension != entity.Pos.Dimension) continue;
                if (plrEntity.Pos.DistanceTo(entity.Pos) <= range)
                {
                    count++;
                }
            }
            return count;
        }

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;
            
            MarkCooldownStart();
            long now = Sapi.World.ElapsedMilliseconds;
            
            // Check which ability to use
            bool canShockwave = now - lastShockwaveMs >= stage.shockwaveCooldownMs;
            bool canSlam = now - lastSlamMs >= stage.slamCooldownMs;

            if (canShockwave)
            {
                PerformShockwave(stage, now);
            }
            else if (canSlam)
            {
                PerformSlam(stage, now);
            }
        }

        private void PerformShockwave(Stage stage, long now)
        {
            lastShockwaveMs = now;

            // Push all nearby players back
            var players = Sapi.World.AllOnlinePlayers;
            for (int i = 0; i < players.Length; i++)
            {
                var plrEntity = players[i].Entity;
                if (plrEntity?.Pos == null) continue;
                if (plrEntity.Pos.Dimension != entity.Pos.Dimension) continue;

                double dist = plrEntity.Pos.DistanceTo(entity.Pos);
                if (dist > stage.knockbackRange) continue;

                // Calculate knockback
                Vec3d dir = plrEntity.Pos.XYZ - entity.Pos.XYZ;
                dir.Normalize();
                dir.Y = 0.5;
                dir.Mul(0.6);

                plrEntity.Pos.Motion.Add(dir);
            }

            // Visual effect
            var entityPos = entity.Pos.XYZ;
            Sapi.World.SpawnParticles(
                new SimpleParticleProperties(
                    60, 80,
                    ColorUtil.ToRgba(200, 200, 200, 255),
                    entityPos.Add(-stage.knockbackRange, 0, -stage.knockbackRange),
                    entityPos.Add(stage.knockbackRange, 2, stage.knockbackRange),
                    new Vec3f(-0.3f, 0.1f, -0.3f),
                    new Vec3f(0.3f, 0.3f, 0.3f),
                    0.8f,
                    0.1f,
                    0.5f,
                    0.5f,
                    EnumParticleModel.Quad
                )
            );

            // Sound
            Sapi.World.PlaySoundAt(
                new AssetLocation("albase:shock-sound-effect-horror"),
                entity.Pos.X, entity.Pos.Y, entity.Pos.Z,
                null, false, 32, 0.5f
            );
        }

        private void PerformSlam(Stage stage, long now)
        {
            lastSlamMs = now;

            // Damage and stun all nearby players
            var players = Sapi.World.AllOnlinePlayers;
            for (int i = 0; i < players.Length; i++)
            {
                var plrEntity = players[i].Entity;
                if (plrEntity?.Pos == null) continue;
                if (plrEntity.Pos.Dimension != entity.Pos.Dimension) continue;

                double dist = plrEntity.Pos.DistanceTo(entity.Pos);
                if (dist > stage.maxTargetRange * 1.5) continue;

                // Damage
                plrEntity.ReceiveDamage(
                    new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.BluntAttack
                    },
                    stage.slamDamage
                );

                // Stun
                plrEntity.WatchedAttributes.SetLong(
                    "alegacyvsquest:stununtil",
                    Sapi.World.ElapsedMilliseconds + stage.stunMs
                );
            }

            // Visual effect
            var slamPos = entity.Pos.XYZ;
            Sapi.World.SpawnParticles(
                new SimpleParticleProperties(
                    40, 60,
                    ColorUtil.ToRgba(255, 150, 100, 50),
                    slamPos.Add(-3, 0, -3),
                    slamPos.Add(3, 1, 3),
                    new Vec3f(-0.3f, 0.2f, -0.3f),
                    new Vec3f(0.3f, 0.5f, 0.3f),
                    0.6f,
                    0.3f,
                    0.5f,
                    0.5f,
                    EnumParticleModel.Quad
                )
            );
        }
    }
}
