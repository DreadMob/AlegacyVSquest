using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossDamageShield : BossAbilityBase
    {
        private const string ShieldStageKey = "alegacyvsquest:bossdamageshield:stage";

        protected override string CooldownKey => "alegacyvsquest:bossdamageshield:lastStartMs";
        protected override bool UseHealthBasedStages() => false;
        protected override bool RequiresTarget() => false;
        protected override int CheckIntervalMs => 200;

        private class Stage : BossAbilityStage
        {
            public int shieldMs;
            public int windupMs;

            public bool repeatable;
            public bool immobileDuringShield;
            public bool lockYawDuringShield;

            public float incomingDamageMultiplier;

            public string animation;
            public int animationStopMs;

            public string sound;
            public float soundRange;
            public int soundStartMs;

            public string loopSound;
            public float loopSoundRange;
            public int loopSoundIntervalMs;

            // Particle settings
            public int particleColorRgba;
            public int particleCountMin;
            public int particleCountMax;
            public float particleSize;
            public int particleLifeMs;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                shieldMs = json["shieldMs"].AsInt(2500);
                windupMs = json["windupMs"].AsInt(0);
                repeatable = json["repeatable"].AsBool(false);
                immobileDuringShield = json["immobile"].AsBool(false);
                lockYawDuringShield = json["lockYaw"].AsBool(false);
                incomingDamageMultiplier = json["incomingDamageMultiplier"].AsFloat(0.25f);
                animation = json["animation"].AsString(null);
                animationStopMs = json["animationStopMs"].AsInt(0);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(24f);
                soundStartMs = json["soundStartMs"].AsInt(0);
                loopSound = json["loopSound"].AsString(null);
                loopSoundRange = json["loopSoundRange"].AsFloat(24f);
                loopSoundIntervalMs = json["loopSoundIntervalMs"].AsInt(900);
                particleColorRgba = json["particleColorRgba"].AsInt(0x6496DCFF);
                particleCountMin = json["particleCountMin"].AsInt(3);
                particleCountMax = json["particleCountMax"].AsInt(6);
                particleSize = json["particleSize"].AsFloat(0.5f);
                particleLifeMs = json["particleLifeMs"].AsInt(800);

                if (shieldMs <= 0) shieldMs = 500;
                if (windupMs < 0) windupMs = 0;
                if (incomingDamageMultiplier < 0f) incomingDamageMultiplier = 0f;
                if (incomingDamageMultiplier > 1f) incomingDamageMultiplier = 1f;
            }
        }

        private List<Stage> stages = new List<Stage>();

        private long shieldEndsAtMs;
        private long shieldStartedAtMs;
        private int activeStageIndex = -1;

        private long startShieldCallbackId;
        private readonly BossBehaviorUtils.LoopSound loopSoundPlayer = new BossBehaviorUtils.LoopSound();
        private long particleCallbackId;

        private bool immobileDuringShield;
        private bool lockYawDuringShield;
        private bool yawLocked;
        private float lockedYaw;

        public EntityBehaviorBossDamageShield(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossdamageshield";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        protected override bool ShouldCheckAbility() => !IsAbilityActive;

        protected override void StopAbility()
        {
            StopShield();
        }

        protected override bool OnAbilityTick(float dt)
        {
            if (!IsAbilityActive) return false;

            if (immobileDuringShield)
            {
                BossBehaviorUtils.StopAiAndFreeze(entity);
            }

            if (lockYawDuringShield)
            {
                BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);
            }

            if (Sapi.World.ElapsedMilliseconds >= shieldEndsAtMs)
            {
                return false;
            }

            return true;
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (!IsAbilityActive) return;
            if (activeStageIndex < 0 || activeStageIndex >= stages.Count) return;

            float mult = stages[activeStageIndex].incomingDamageMultiplier;
            if (mult >= 0f && mult < 0.9999f)
            {
                damage *= mult;
            }
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            StopShield();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopShield();
            base.OnEntityDespawn(despawn);
        }

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;

            MarkCooldownStart();

            if (!stage.repeatable)
            {
                entity.WatchedAttributes.SetInt(ShieldStageKey, stageIndex + 1);
                entity.WatchedAttributes.MarkPathDirty(ShieldStageKey);
            }

            StartShield(stage, stageIndex);
        }

        private void StartShield(Stage stage, int stageIndex)
        {
            if (IsAbilityActive) return;

            SetAbilityActive(true);
            activeStageIndex = stageIndex;
            immobileDuringShield = stage.immobileDuringShield;
            lockYawDuringShield = stage.lockYawDuringShield;
            yawLocked = false;

            shieldStartedAtMs = Sapi.World.ElapsedMilliseconds;
            shieldEndsAtMs = shieldStartedAtMs + Math.Max(200, stage.windupMs + stage.shieldMs);

            UnregisterCallbackSafe(ref startShieldCallbackId);

            TryPlaySound(stage);
            TryStartLoopSound(stage);
            StartParticles(stage);

            if (immobileDuringShield)
            {
                BossBehaviorUtils.StopAiAndFreeze(entity);
            }

            if (lockYawDuringShield)
            {
                BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);
            }

            if (stage.windupMs > 0)
            {
                startShieldCallbackId = Sapi.Event.RegisterCallback(_ =>
                {
                    TryPlayAnimationInternal(stage);
                }, stage.windupMs);
            }
            else
            {
                TryPlayAnimationInternal(stage);
            }
        }

        private void StopShield()
        {
            UnregisterCallbackSafe(ref startShieldCallbackId);
            UnregisterCallbackSafe(ref particleCallbackId);

            SetAbilityActive(false);
            immobileDuringShield = false;
            lockYawDuringShield = false;
            yawLocked = false;

            shieldStartedAtMs = 0;
            shieldEndsAtMs = 0;

            loopSoundPlayer.Stop();

            if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
            {
                var stage = stages[activeStageIndex];
                if (!string.IsNullOrWhiteSpace(stage.animation))
                {
                    entity?.AnimManager?.StopAnimation(stage.animation);
                }
            }

            activeStageIndex = -1;
        }

        private void TryPlayAnimationInternal(Stage stage)
        {
            if (stage == null || string.IsNullOrWhiteSpace(stage.animation)) return;

            TryPlayAnimation(stage.animation);

            int stopMs = stage.animationStopMs;
            if (stopMs <= 0) return;

            if (Sapi != null)
            {
                Sapi.Event.RegisterCallback(_ =>
                {
                    entity?.AnimManager?.StopAnimation(stage.animation);
                }, stopMs);
            }
        }

        private void TryPlaySound(Stage stage)
        {
            if (Sapi == null || stage == null || string.IsNullOrWhiteSpace(stage.sound)) return;
            float range = stage.soundRange > 0f ? stage.soundRange : 24f;

            AssetLocation soundLoc = AssetLocation.Create(stage.sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            if (stage.soundStartMs > 0)
            {
                Sapi.Event.RegisterCallback(_ =>
                {
                    if (!IsAbilityActive || entity == null || !entity.Alive) return;
                    float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                    Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, range, 1f);
                }, stage.soundStartMs);
            }
            else
            {
                if (!IsAbilityActive || entity == null || !entity.Alive) return;
                float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, range, 1f);
            }
        }

        private void TryStartLoopSound(Stage stage)
        {
            if (Sapi == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.loopSound)) return;

            loopSoundPlayer.Start(Sapi, entity, stage.loopSound, stage.loopSoundRange, stage.loopSoundIntervalMs);
        }

        private void StartParticles(Stage stage)
        {
            if (Sapi == null || entity == null || stage == null) return;
            if (stage.particleCountMax <= 0) return;

            SpawnShieldParticles(stage);

            // Schedule recurring particles while shield is active
            particleCallbackId = Sapi.Event.RegisterCallback(_ =>
            {
                if (IsAbilityActive && entity != null && entity.Alive)
                {
                    SpawnShieldParticles(stage);
                }
            }, 400);
        }

        private void SpawnShieldParticles(Stage stage)
        {
            if (Sapi == null || entity == null || stage == null) return;

            var pos = entity.Pos.XYZ.Add(0, 1.5, 0); // Center of boss

            ParticleUtils.Create()
                .Count(stage.particleCountMin, stage.particleCountMax)
                .Color(stage.particleColorRgba)
                .Position(pos, 0.5f)
                .VelocityUp(0.5f, 1.5f)
                .Life(stage.particleLifeMs / 1000f)
                .Size(stage.particleSize, stage.particleSize)
                .Spawn(Sapi);
        }

        // Required abstract overrides for BossAbilityBase (event-driven mode)
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => index >= 0 && index < stages.Count ? stages[index] : null;
        protected override float GetStageHealthThreshold(object stage) => stage is Stage s ? s.whenHealthRelBelow : 1f;
        protected override float GetStageCooldown(object stage) => stage is Stage s ? s.cooldownSeconds : 0f;
        protected override float GetMaxTargetRange(object stage) => 0f;
    }
}
