using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossCastPhase : BossAbilityBase
    {
        private const string CastStageKey = "alegacyvsquest:bosscastphase:stage";

        protected override string CooldownKey => "alegacyvsquest:bosscastphase:lastStartMs";
        protected override bool UseHealthBasedStages() => false;
        protected override bool RequiresTarget() => false;
        protected override int CheckIntervalMs => 500;

        private class Stage : BossAbilityStage
        {
            public int castMs;
            public int windupMs;

            public float healPerSecond;
            public float healRelPerSecond;
            public float incomingDamageMultiplier;

            public string animation;
            public string sound;
            public float soundRange;
            public int soundStartMs;

            public string loopSound;
            public float loopSoundRange;
            public int loopSoundIntervalMs;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                castMs = json["castMs"].AsInt(2500);
                windupMs = json["windupMs"].AsInt(0);
                healPerSecond = json["healPerSecond"].AsFloat(0f);
                healRelPerSecond = json["healRelPerSecond"].AsFloat(0f);
                incomingDamageMultiplier = json["incomingDamageMultiplier"].AsFloat(1f);
                animation = json["animation"].AsString(null);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(24f);
                soundStartMs = json["soundStartMs"].AsInt(0);
                loopSound = json["loopSound"].AsString(null);
                loopSoundRange = json["loopSoundRange"].AsFloat(24f);
                loopSoundIntervalMs = json["loopSoundIntervalMs"].AsInt(900);

                if (castMs <= 0) castMs = 500;
                if (windupMs < 0) windupMs = 0;
                if (incomingDamageMultiplier < 0f) incomingDamageMultiplier = 0f;
                if (incomingDamageMultiplier > 1f) incomingDamageMultiplier = 1f;
            }
        }

        private List<Stage> stages = new List<Stage>();

        private long castEndsAtMs;
        private long castStartedAtMs;
        private float lockedYaw;
        private bool yawLocked;
        private int activeStageIndex = -1;

        private readonly BossBehaviorUtils.LoopSound loopSoundPlayer = new BossBehaviorUtils.LoopSound();

        public EntityBehaviorBossCastPhase(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosscastphase";

        public bool IsCasting => IsAbilityActive;

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        protected override bool ShouldCheckAbility() => !IsAbilityActive;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;

            MarkCooldownStart();
            entity.WatchedAttributes.SetInt(CastStageKey, stageIndex + 1);
            entity.WatchedAttributes.MarkPathDirty(CastStageKey);
            StartCast(stage, stageIndex);
        }

        protected override void StopAbility()
        {
            StopCast();
        }

        protected override bool OnAbilityTick(float dt)
        {
            if (!IsAbilityActive) return false;

            if (Sapi.World.ElapsedMilliseconds >= castEndsAtMs)
            {
                return false;
            }

            entity.ApplyRotationLock(ref yawLocked, ref lockedYaw);
            TickCast(dt);

            return true;
        }

        private void StartCast(Stage stage, int stageIndex)
        {
            if (IsAbilityActive) return;

            SetAbilityActive(true);
            activeStageIndex = stageIndex;
            castStartedAtMs = Sapi.World.ElapsedMilliseconds;
            castEndsAtMs = castStartedAtMs + Math.Max(200, stage.windupMs + stage.castMs);

            yawLocked = false;
            entity.StopAiAndFreeze();
            entity.ApplyRotationLock(ref yawLocked, ref lockedYaw);

            TryPlaySound(stage);
            TryStartLoopSound(stage);

            if (stage.windupMs > 0)
            {
                RegisterCallbackTracked(_ =>
                {
                    if (IsAbilityActive) TryPlayAnimation(stage.animation);
                }, stage.windupMs);
            }
            else
            {
                TryPlayAnimation(stage.animation);
            }
        }

        private void TickCast(float dt)
        {
            if (activeStageIndex < 0 || activeStageIndex >= stages.Count) return;
            var stage = stages[activeStageIndex];

            // Heal boss
            if (!entity.TryGetHealth(out var healthTree, out float curHealth, out float maxHealth)) return;

            float healAbs = stage.healPerSecond * dt;
            float healRel = stage.healRelPerSecond * maxHealth * dt;
            float heal = healAbs + healRel;

            if (heal > 0f)
            {
                float newHealth = Math.Min(maxHealth, curHealth + heal);
                healthTree.SetFloat("currenthealth", newHealth);
                entity.WatchedAttributes.MarkPathDirty("health");
            }
        }

        private void StopCast()
        {
            SetAbilityActive(false);
            yawLocked = false;
            loopSoundPlayer.Stop();

            if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
            {
                TryStopAnimation(stages[activeStageIndex].animation);
            }

            activeStageIndex = -1;
            castStartedAtMs = 0;
            castEndsAtMs = 0;
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
            StopAbility();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopAbility();
            base.OnEntityDespawn(despawn);
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

        // Required abstract overrides for BossAbilityBase
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => index >= 0 && index < stages.Count ? stages[index] : null;
        protected override float GetStageHealthThreshold(object stage) => stage is Stage s ? s.whenHealthRelBelow : 1f;
        protected override float GetStageCooldown(object stage) => stage is Stage s ? s.cooldownSeconds : 0f;
        protected override float GetMaxTargetRange(object stage) => 0f;
    }
}
