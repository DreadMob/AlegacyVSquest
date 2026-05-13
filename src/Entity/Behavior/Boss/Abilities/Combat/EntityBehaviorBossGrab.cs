using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossGrab : BossAbilityBase
    {
        private const string VictimNoSneakUntilKey = "alegacyvsquest:bossgrab:nosneakuntil";
        private const string WalkSpeedStatCode = "alegacyvsquest:bossgrab";
        private const string WalkSpeedStatCodeLegacy = "alegacyvsquest";

        protected override string CooldownKey => "alegacyvsquest:bossgrab:lastStartMs";

        private class GrabStage : BossAbilityStage
        {
            public int windupMs;
            public int grabMs;
            public float victimWalkSpeedMult;

            public int damageIntervalMs;
            public float damage;
            public int damageTier;
            public string damageType;

            public string windupAnimation;
            public string grabAnimation;

            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                windupMs = json["windupMs"].AsInt(150);
                grabMs = json["grabMs"].AsInt(2500);
                victimWalkSpeedMult = json["victimWalkSpeedMult"].AsFloat(0.08f);
                damageIntervalMs = json["damageIntervalMs"].AsInt(500);
                damage = json["damage"].AsFloat(2f);
                damageTier = json["damageTier"].AsInt(3);
                damageType = json["damageType"].AsString("PiercingAttack");
                windupAnimation = json["windupAnimation"].AsString(null);
                grabAnimation = json["grabAnimation"].AsString(null);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(24f);
                soundStartMs = json["soundStartMs"].AsInt(0);
                soundVolume = json["soundVolume"].AsFloat(1f);

                // Validation
                if (windupMs < 0) windupMs = 0;
                if (grabMs <= 0) grabMs = 250;
                if (victimWalkSpeedMult < 0f) victimWalkSpeedMult = 0f;
                if (damageIntervalMs <= 0) damageIntervalMs = 250;
                if (damage < 0f) damage = 0f;
                if (damageTier < 0) damageTier = 0;
                if (soundVolume <= 0f) soundVolume = 1f;
            }
        }

        private List<GrabStage> stages = new List<GrabStage>();

        private long grabEndsAtMs;
        private long grabStartedAtMs;
        private long grabStartCallbackId;
        private long grabTickListenerId;

        private long grabEffectsStartAtMs;
        private GrabStage activeStage;

        private int activeStageIndex = -1;
        private EntityPlayer targetPlayer;

        private long nextDamageAtMs;

        public EntityBehaviorBossGrab(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossgrab";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<GrabStage>(attributes);
        }

        protected override int GetStageCount() => stages.Count;

        protected override object GetStage(int index) => stages[index];

        protected override float GetStageHealthThreshold(object stage) => ((GrabStage)stage).whenHealthRelBelow;

        protected override float GetStageCooldown(object stage) => ((GrabStage)stage).cooldownSeconds;

        protected override float GetMaxTargetRange(object stage) => ((GrabStage)stage).maxTargetRange;

        protected override bool ShouldCheckAbility() => !IsAbilityActive;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (target == null || stageObj is not GrabStage stage) return;
            StartGrab(stage, stageIndex, target);
        }

        protected override void StopAbility() => StopGrab();

        protected override bool OnAbilityTick(float dt)
        {
            if (!IsAbilityActive) return false;
            if (Sapi == null || entity == null) return false;

            long now = Sapi.World.ElapsedMilliseconds;

            if (now < grabEffectsStartAtMs)
            {
                return true;
            }

            if (now >= grabEndsAtMs)
            {
                return false;
            }

            if (!entity.Alive) return false;
            if (targetPlayer == null || !targetPlayer.Alive) return false;
            if (targetPlayer.Pos.Dimension != entity.Pos.Dimension) return false;
            if (activeStage == null) return false;

            float dist = (float)targetPlayer.Pos.DistanceTo(entity.Pos);
            if (dist > activeStage.maxTargetRange + 2f)
            {
                return false;
            }

            ApplyVictimMoveSlow(activeStage);

            if (now >= nextDamageAtMs)
            {
                DealGrabDamage(activeStage);
                nextDamageAtMs = now + Math.Max(250, activeStage.damageIntervalMs);
            }

            return true;
        }

        private void StartGrab(GrabStage stage, int index, EntityPlayer target)
        {
            if (Sapi == null || entity == null || stage == null || target == null) return;

            MarkCooldownStart();
            SetAbilityActive(true);

            activeStageIndex = index;
            activeStage = stage;
            grabStartedAtMs = Sapi.World.ElapsedMilliseconds;
            targetPlayer = target;

            UnregisterCallbackSafe(ref grabStartCallbackId);
            UnregisterGameTickListenerSafe(ref grabTickListenerId);

            BossBehaviorUtils.StopAiAndFreeze(entity);

            TryPlaySound(stage.sound, stage.soundRange, stage.soundStartMs, stage.soundVolume);
            TryPlayAnimation(stage.windupAnimation);

            int windup = Math.Max(0, stage.windupMs);
            int grabMs = Math.Max(100, stage.grabMs);
            grabEndsAtMs = grabStartedAtMs + windup + grabMs;
            grabEffectsStartAtMs = grabStartedAtMs + windup;

            if (windup > 0)
            {
                grabStartCallbackId = Sapi.Event.RegisterCallback(_ =>
                {
                    BeginGrab(stage);
                }, windup);
            }
            else
            {
                BeginGrab(stage);
            }
        }

        private void BeginGrab(GrabStage stage)
        {
            if (Sapi == null || entity == null || stage == null) return;

            TryPlayAnimation(stage.grabAnimation);

            // Grab impact particles on victim
            if (targetPlayer != null)
            {
                ParticleUtils.SpawnImpact(Sapi, targetPlayer, ParticleUtils.Colors.Blood, 12, 0.3f);
            }

            ApplyVictimMoveSlow(stage);

            if (targetPlayer?.WatchedAttributes != null)
            {
                targetPlayer.WatchedAttributes.SetLong(VictimNoSneakUntilKey, grabEndsAtMs);
                targetPlayer.WatchedAttributes.MarkPathDirty(VictimNoSneakUntilKey);
            }

            nextDamageAtMs = Sapi.World.ElapsedMilliseconds;
        }

        private void ApplyVictimMoveSlow(GrabStage stage)
        {
            if (stage == null || targetPlayer?.Stats == null) return;

            float mult = GameMath.Clamp(stage.victimWalkSpeedMult, 0f, 1f);
            float modifier = mult - 1f;

            targetPlayer.Stats.Remove("walkspeed", WalkSpeedStatCodeLegacy);
            targetPlayer.Stats.Set("walkspeed", WalkSpeedStatCode, modifier, true);

            targetPlayer.walkSpeed = targetPlayer.Stats.GetBlended("walkspeed");
        }

        private void DealGrabDamage(GrabStage stage)
        {
            if (stage == null || stage.damage <= 0f) return;
            if (targetPlayer == null || !targetPlayer.Alive) return;
            if (entity == null) return;

            EnumDamageType dmgType = EnumDamageType.PiercingAttack;
            if (!string.IsNullOrWhiteSpace(stage.damageType) && Enum.TryParse(stage.damageType, ignoreCase: true, out EnumDamageType parsed))
            {
                dmgType = parsed;
            }

            targetPlayer.ReceiveDamage(new DamageSource()
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = entity,
                Type = dmgType,
                DamageTier = stage.damageTier,
                KnockbackStrength = 0f
            }, stage.damage);
        }

        private void StopGrab()
        {
            UnregisterCallbackSafe(ref grabStartCallbackId);
            UnregisterGameTickListenerSafe(ref grabTickListenerId);

            if (!IsAbilityActive && activeStageIndex < 0) return;

            SetAbilityActive(false);

            RestoreVictimMoveSpeed();

            if (targetPlayer?.WatchedAttributes != null)
            {
                targetPlayer.WatchedAttributes.SetLong(VictimNoSneakUntilKey, 0);
                targetPlayer.WatchedAttributes.MarkPathDirty(VictimNoSneakUntilKey);
            }

            targetPlayer = null;

            activeStage = null;

            grabStartedAtMs = 0;
            grabEndsAtMs = 0;
            grabEffectsStartAtMs = 0;
            nextDamageAtMs = 0;

            if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
            {
                var stage = stages[activeStageIndex];

                if (!string.IsNullOrWhiteSpace(stage.windupAnimation))
                {
                    entity?.AnimManager?.StopAnimation(stage.windupAnimation);
                }

                if (!string.IsNullOrWhiteSpace(stage.grabAnimation))
                {
                    entity?.AnimManager?.StopAnimation(stage.grabAnimation);
                }
            }

            activeStageIndex = -1;
        }

        private void RestoreVictimMoveSpeed()
        {
            if (targetPlayer?.Stats == null) return;

            targetPlayer.Stats.Remove("walkspeed", WalkSpeedStatCode);
            targetPlayer.Stats.Remove("walkspeed", WalkSpeedStatCodeLegacy);
            BossBehaviorUtils.UpdatePlayerWalkSpeed(targetPlayer);
        }

    }
}
