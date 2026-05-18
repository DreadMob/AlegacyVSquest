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
    public class EntityBehaviorBossDash : BossAbilityBase
    {
        protected override string CooldownKey => "alegacyvsquest:bossdash:lastStartMs";

        private class DashStage : BossAbilityStage
        {
            public int windupMs;
            public int dashMs;
            public float dashSpeed;
            public string dashDirection;
            public string windupAnimation;
            public string dashAnimation;
            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                windupMs = json["windupMs"].AsInt(350);
                dashMs = json["dashMs"].AsInt(650);
                dashSpeed = json["dashSpeed"].AsFloat(0.18f);
                dashDirection = json["dashDirection"].AsString("towards");
                windupAnimation = json["windupAnimation"].AsString(null);
                dashAnimation = json["dashAnimation"].AsString(null);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(24f);
                soundStartMs = json["soundStartMs"].AsInt(0);
                soundVolume = json["soundVolume"].AsFloat(1f);

                // Validation
                if (windupMs < 0) windupMs = 0;
                if (dashMs <= 0) dashMs = 250;
                if (dashSpeed <= 0f) dashSpeed = 0.08f;
                if (soundVolume <= 0f) soundVolume = 1f;
            }
        }

        private Vec3d ApplyDashDirection(Vec3d baseDir, string dashDirection)
        {
            if (baseDir == null) return new Vec3d(0, 0, 1);
            string dir = dashDirection?.Trim()?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(dir) || dir == "towards" || dir == "forward") return baseDir;

            Vec3d forward = null;
            if (entity?.Pos != null)
            {
                float yaw = entity.Pos.Yaw;
                forward = new Vec3d(Math.Sin(yaw), 0, Math.Cos(yaw));
                if (forward.Length() < 0.001) forward = null;
            }

            forward ??= baseDir;

            if (dir == "away" || dir == "back" || dir == "backwards")
            {
                return new Vec3d(-forward.X, 0, -forward.Z);
            }

            if (dir == "left")
            {
                return new Vec3d(-forward.Z, 0, forward.X);
            }

            if (dir == "right")
            {
                return new Vec3d(forward.Z, 0, -forward.X);
            }

            if (dir == "side")
            {
                var rng = Sapi?.World?.Rand ?? entity?.World?.Rand;
                bool left = (rng?.NextDouble() ?? 0.0) < 0.5;
                return left
                    ? new Vec3d(-forward.Z, 0, forward.X)
                    : new Vec3d(forward.Z, 0, -forward.X);
            }

            return baseDir;
        }

        private List<DashStage> stages = new List<DashStage>();

        private long dashEndsAtMs;
        private long dashStartedAtMs;
        private long dashStartCallbackId;
        private long dashTickListenerId;

        private long dashMoveStartsAtMs;

        private float lockedYaw;
        private bool yawLocked;
        private int activeStageIndex = -1;

        private Entity targetEntity;
        private Vec3d dashDir;

        private DashStage activeStage;

        public EntityBehaviorBossDash(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossdash";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<DashStage>(attributes);
        }

        protected override int GetStageCount() => stages.Count;

        protected override object GetStage(int index) => stages[index];

        protected override float GetStageHealthThreshold(object stage) => ((DashStage)stage).whenHealthRelBelow;

        protected override float GetStageCooldown(object stage) => ((DashStage)stage).cooldownSeconds;

        protected override float GetMaxTargetRange(object stage) => ((DashStage)stage).maxTargetRange;

        protected override float MinTargetRange => 0.75f;

        protected override bool ShouldCheckAbility() => !IsAbilityActive;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (target == null || stageObj is not DashStage stage) return;
            StartDash(stage, stageIndex, target);
        }

        protected override void StopAbility() => StopDash();

        protected override bool OnAbilityTick(float dt)
        {
            if (!IsAbilityActive) return false;

            BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);

            long now = Sapi.World.ElapsedMilliseconds;

            if (now < dashMoveStartsAtMs)
            {
                return true;
            }

            if (now >= dashEndsAtMs)
            {
                return false;
            }

            if (entity == null || !entity.Alive) return false;
            if (activeStage == null) return false;
            if (targetEntity == null || !targetEntity.Alive) return false;
            if (targetEntity.Pos.Dimension != entity.Pos.Dimension) return false;

            dashDir.Set(targetEntity.Pos.X - entity.Pos.X, 0, targetEntity.Pos.Z - entity.Pos.Z);
            if (dashDir.Length() < 0.001) return true;
            dashDir.Normalize();

            lockedYaw = (float)Math.Atan2(dashDir.X, dashDir.Z);
            yawLocked = true;

            double spd = activeStage.dashSpeed;
            entity.Pos.Motion.X = dashDir.X * spd;
            entity.Pos.Motion.Z = dashDir.Z * spd;

            return true;
        }

        private void StartDash(DashStage stage, int stageIndex, Entity target)
        {
            if (Sapi == null || entity == null || stage == null || target == null) return;

            MarkCooldownStart();
            SetAbilityActive(true);
            activeStageIndex = stageIndex;
            activeStage = stage;
            dashStartedAtMs = Sapi.World.ElapsedMilliseconds;
            targetEntity = target;

            UnregisterCallbackSafe(ref dashStartCallbackId);
            UnregisterGameTickListenerSafe(ref dashTickListenerId);

            BossBehaviorUtils.StopAiAndFreeze(entity);
            BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);

            TryPlaySound(stage.sound, stage.soundRange, stage.soundStartMs, stage.soundVolume);
            TryPlayAnimation(stage.windupAnimation);

            dashDir = new Vec3d(target.Pos.X - entity.Pos.X, 0, target.Pos.Z - entity.Pos.Z);
            if (dashDir.Length() < 0.001) dashDir.Set(0, 0, 1);
            dashDir = ApplyDashDirection(dashDir, stage?.dashDirection);
            dashDir.Normalize();

            lockedYaw = (float)Math.Atan2(dashDir.X, dashDir.Z);
            yawLocked = true;

            int windup = Math.Max(0, stage.windupMs);
            int dashMs = Math.Max(100, stage.dashMs);
            dashEndsAtMs = dashStartedAtMs + windup + dashMs;
            dashMoveStartsAtMs = dashStartedAtMs + windup;

            if (windup > 0)
            {
                dashStartCallbackId = Sapi.Event.RegisterCallback(_ =>
                {
                    BeginDash(stage);
                }, windup);
            }
            else
            {
                BeginDash(stage);
            }
        }

        private void BeginDash(DashStage stage)
        {
            if (entity == null || stage == null) return;

            TryPlayAnimation(stage.dashAnimation);

            // Dash trail particles at start — big burst
            if (Sapi != null)
            {
                ParticleUtils.SpawnShockwave(Sapi, entity.Pos.XYZ, 2.5f, ParticleUtils.Colors.Shadow, 20, 0.6f);
            }

            // Register game tick listener for continuous movement updates
            dashTickListenerId = Sapi.Event.RegisterGameTickListener(_ =>
            {
                try
                {
                    if (!IsAbilityActive)
                    {
                        StopDash();
                        return;
                    }

                    long now = Sapi.World.ElapsedMilliseconds;
                    if (now >= dashEndsAtMs)
                    {
                        StopDash();
                        return;
                    }

                    if (entity == null || !entity.Alive)
                    {
                        StopDash();
                        return;
                    }

                    if (activeStage == null) return;
                    if (targetEntity == null || !targetEntity.Alive) return;
                    if (targetEntity.Pos.Dimension != entity.Pos.Dimension) return;

                    dashDir.Set(targetEntity.Pos.X - entity.Pos.X, 0, targetEntity.Pos.Z - entity.Pos.Z);
                    if (dashDir.Length() < 0.001) return;
                    dashDir.Normalize();

                    lockedYaw = (float)Math.Atan2(dashDir.X, dashDir.Z);
                    yawLocked = true;

                    double spd = activeStage.dashSpeed;
                    entity.Pos.Motion.X = dashDir.X * spd;
                    entity.Pos.Motion.Z = dashDir.Z * spd;

                    // Trail particles during dash
                    if (Sapi != null && now % 100 < 60)
                    {
                        ParticleUtils.SpawnEntityAura(Sapi, entity, ParticleUtils.Colors.Shadow, 4, 0.4f, 0.6f);
                    }
                }
                catch (Exception ex)
                {
                    entity?.Api?.Logger?.Error($"[vsquest] Exception in dash tick: {ex}");
                }
            }, 50);
        }

        private void StopDash()
        {
            UnregisterCallbackSafe(ref dashStartCallbackId);
            UnregisterGameTickListenerSafe(ref dashTickListenerId);

            if (!IsAbilityActive && activeStageIndex < 0) return;

            SetAbilityActive(false);
            yawLocked = false;
            targetEntity = null;
            activeStage = null;

            dashStartedAtMs = 0;
            dashEndsAtMs = 0;
            dashMoveStartsAtMs = 0;

            if (entity != null)
            {
                entity.Pos.Motion.Set(0, 0, 0);
            }

            if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
            {
                var stage = stages[activeStageIndex];

                TryStopAnimation(stage.windupAnimation);
                TryStopAnimation(stage.dashAnimation);
            }

            activeStageIndex = -1;
        }
    }
}
