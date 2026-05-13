using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossCorpseExplosion : BossAbilityBase
    {
        private List<Stage> stages = new List<Stage>();
        private float poisonPerSecond;
        private int poisonDurationMs;
        private int cooldownMs;

        protected override string CooldownKey => "alegacyvsquest:bosscorpseexplosion:lastExplosionMs";
        protected override bool UseHealthBasedStages() => false;
        protected override bool RequiresTarget() => false;
        protected override int CheckIntervalMs => 500;

        public EntityBehaviorBossCorpseExplosion(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosscorpseexplosion";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
            poisonPerSecond = attributes["poisonPerSecond"].AsFloat(8f);
            poisonDurationMs = attributes["poisonDurationSeconds"].AsInt(10) * 1000;
            cooldownMs = attributes["cooldownBetweenExplosionsMs"].AsInt(2500);
        }

        protected override bool ShouldCheckAbility()
        {
            return !IsAbilityActive;
        }

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;
            MarkCooldownStart();
            Explode(stage);
        }

        protected override bool OnAbilityTick(float dt)
        {
            return false; // No ongoing state
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            if (Sapi == null) return;
            if (despawn.Reason != EnumDespawnReason.Death) return;

            if (!IsCooldownReady(null)) return;

            MarkCooldownStart();
            PerformExplosion();
        }

        private void Explode(Stage stage)
        {
            var pos = entity.Pos.XYZ;

            // Damage players in radius
            foreach (var player in Sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.Pos == null) continue;
                if (player.Entity.Pos.Dimension != entity.Pos.Dimension) continue;

                double dist = player.Entity.Pos.DistanceTo(pos);
                if (dist > stage.radius) continue;

                // Apply explosion damage
                player.Entity.ReceiveDamage(
                    new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.PiercingAttack
                    },
                    stage.damage
                );

                // Apply poison
                player.Entity.WatchedAttributes.SetFloat(
                    "intoxication",
                    player.Entity.WatchedAttributes.GetFloat("intoxication") + poisonPerSecond
                );
                player.Entity.WatchedAttributes.SetLong(
                    "alegacyvsquest:poisonuntil",
                    Sapi.World.ElapsedMilliseconds + poisonDurationMs
                );
            }

            // Visual explosion + poison cloud via ParticleUtils
            ParticleUtils.SpawnFireExplosion(Sapi, pos, stage.radius);
            ParticleUtils.SpawnPoisonExplosion(Sapi, pos, stage.radius);

            // Sound
            Sapi.World.PlaySoundAt(
                new AssetLocation("effect/smallexplosion"),
                pos.X, pos.Y, pos.Z,
                null, false, 32, 0.5f
            );
        }

        private void PerformExplosion()
        {
            var pos = entity.Pos.XYZ;

            // Damage players in radius
            foreach (var player in Sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.Pos == null) continue;
                if (player.Entity.Pos.Dimension != entity.Pos.Dimension) continue;

                double dist = player.Entity.Pos.DistanceTo(pos);
                if (dist > 12f) continue;

                // Apply explosion damage
                player.Entity.ReceiveDamage(
                    new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.PiercingAttack
                    },
                    30f
                );

                // Apply poison
                player.Entity.WatchedAttributes.SetFloat(
                    "intoxication",
                    player.Entity.WatchedAttributes.GetFloat("intoxication") + poisonPerSecond
                );
                player.Entity.WatchedAttributes.SetLong(
                    "alegacyvsquest:poisonuntil",
                    Sapi.World.ElapsedMilliseconds + poisonDurationMs
                );
            }

            // Visual explosion + poison cloud via ParticleUtils (death explosion is larger)
            ParticleUtils.SpawnFireExplosion(Sapi, pos, 12f, 2);
            ParticleUtils.SpawnPoisonExplosion(Sapi, pos, 12f, 2);

            // Sound
            Sapi.World.PlaySoundAt(
                new AssetLocation("effect/smallexplosion"),
                pos.X, pos.Y, pos.Z,
                null, false, 32, 0.5f
            );
        }

        private class Stage : BossAbilityStage
        {
            public int fuseMs;
            public float explosionRadius;
            public float explosionDamage;
            public int damageTier;
            public string damageType;
            public AssetLocation explodeSound;
            public float explodeSoundVolume;

            public float damage;
            public float radius;
            public string animation;
            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                fuseMs = json["fuseMs"].AsInt(2000);
                explosionRadius = json["explosionRadius"].AsFloat(3f);
                explosionDamage = json["explosionDamage"].AsFloat(10f);
                damageTier = json["damageTier"].AsInt(1);
                damageType = json["damageType"].AsString("BluntAttack");

                string soundPath = json["explodeSound"].AsString("effect/smallexplosion");
                explodeSound = new AssetLocation(soundPath);
                explodeSoundVolume = json["explodeSoundVolume"].AsFloat(0.5f);

                damage = json["damage"].AsFloat(15f);
                radius = json["radius"].AsFloat(4f);
                animation = json["animation"].AsString(null);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(24f);
                soundStartMs = json["soundStartMs"].AsInt(0);
                soundVolume = json["soundVolume"].AsFloat(1f);
            }
        }

        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => 0f;

        protected override void StopAbility()
        {
        }
    }
}
