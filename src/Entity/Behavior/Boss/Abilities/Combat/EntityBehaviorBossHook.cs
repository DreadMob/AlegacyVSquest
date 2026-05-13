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
    public class EntityBehaviorBossHook : BossAbilityBase
    {
        protected override string CooldownKey => "alegacyvsquest:bosshook:lastStartMs";

        private const long PullLogIntervalMs = 250;

        private const string WalkSpeedStatCodeHook = "alegacyvsquest:bosshook";
        private const float HookVictimWalkSpeedMult = 0.05f;

        private class HookStage : BossAbilityStage
        {
            public int windupMs;
            public int pullMs;
            public float pullSpeed;
            public float maxPlayerMotion;

            public string windupAnimation;
            public string pullAnimation;

            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                windupMs = json["windupMs"].AsInt(250);
                pullMs = json["pullMs"].AsInt(850);
                pullSpeed = json["pullSpeed"].AsFloat(0.12f);
                maxPlayerMotion = json["maxPlayerMotion"].AsFloat(0.22f);
                windupAnimation = json["windupAnimation"].AsString(null);
                pullAnimation = json["pullAnimation"].AsString(null);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(24f);
                soundStartMs = json["soundStartMs"].AsInt(0);
                soundVolume = json["soundVolume"].AsFloat(1f);

                // Validation
                if (windupMs < 0) windupMs = 0;
                if (pullMs <= 0) pullMs = 250;
                if (pullSpeed <= 0f) pullSpeed = 0.08f;
                if (maxPlayerMotion <= 0f) maxPlayerMotion = 0.18f;
                if (soundVolume <= 0f) soundVolume = 1f;
            }
        }

        private List<HookStage> stages = new List<HookStage>();

        private long hookEndsAtMs;
        private long hookStartedAtMs;
        private long hookStartCallbackId;
        private long hookTickListenerId;

        private long pullStartsAtMs;
        private HookStage activeStage;

        private long lastPullLogAtMs;

        private int activeStageIndex = -1;
        private EntityPlayer targetPlayer;

        public EntityBehaviorBossHook(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosshook";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<HookStage>(attributes);
        }

        protected override int GetStageCount() => stages.Count;

        protected override object GetStage(int index) => stages[index];

        protected override float GetStageHealthThreshold(object stage) => ((HookStage)stage).whenHealthRelBelow;

        protected override float GetStageCooldown(object stage) => ((HookStage)stage).cooldownSeconds;

        protected override float GetMaxTargetRange(object stage) => ((HookStage)stage).maxTargetRange;

        protected override bool ShouldCheckAbility() => !IsAbilityActive;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (target == null || stageObj is not HookStage stage) return;
            StartHook(stage, stageIndex, target);
        }

        protected override void StopAbility() => StopHook();

        protected override bool OnAbilityTick(float dt)
        {
            if (!IsAbilityActive) return false;
            if (Sapi == null || entity == null) return false;

            long now = Sapi.World.ElapsedMilliseconds;

            if (now < pullStartsAtMs)
            {
                return true;
            }

            if (now >= hookEndsAtMs)
            {
                return false;
            }

            if (!entity.Alive) return false;
            if (targetPlayer == null || !targetPlayer.Alive) return false;
            if (targetPlayer.Pos.Dimension != entity.Pos.Dimension) return false;
            if (activeStage == null) return false;

            var dir = new Vec3d(entity.Pos.X - targetPlayer.Pos.X, 0, entity.Pos.Z - targetPlayer.Pos.Z);
            if (dir.Length() < 0.001) return true;

            float dist = (float)targetPlayer.Pos.DistanceTo(entity.Pos);
            dir.Normalize();

            double pull = activeStage.pullSpeed;
            if (pull <= 0.0001) pull = 0.05;
            if (pull < 0.18) pull = 0.18;

            double max = activeStage.maxPlayerMotion;
            if (max <= 0.0001) max = 0.25;
            if (max < 0.35) max = 0.35;

            float maxRange = activeStage.maxTargetRange > 0 ? activeStage.maxTargetRange : 28f;
            float denom = Math.Max(1f, maxRange - 1f);
            float distNorm = GameMath.Clamp((dist - 1f) / denom, 0f, 1f);
            double scale = 1.0 + distNorm * 2.0;
            pull *= scale;
            max *= scale;

            double kbX = GameMath.Clamp(dir.X * pull, -max, max);
            double kbZ = GameMath.Clamp(dir.Z * pull, -max, max);

            long nowMs = now;
            if (lastPullLogAtMs == 0 || nowMs - lastPullLogAtMs >= PullLogIntervalMs)
            {
                lastPullLogAtMs = nowMs;
                Sapi.Logger.VerboseDebug($"[alegacyvsquest] bosshook pull target={targetPlayer?.Player?.PlayerName ?? "?"} kbX={kbX:0.###} kbZ={kbZ:0.###} dist={dist:0.00}");
            }

            // Use vanilla knockback module so the pull cannot be overridden by player client controls.
            // PModuleKnockback reads kbdirX/Y/Z from WatchedAttributes and applies when Attributes.dmgkb == 1.
            targetPlayer.WatchedAttributes.SetDouble("kbdirX", kbX);
            targetPlayer.WatchedAttributes.SetDouble("kbdirY", 0.0);
            targetPlayer.WatchedAttributes.SetDouble("kbdirZ", kbZ);

            targetPlayer.WatchedAttributes.SetFloat("onHurt", 0.01f);
            targetPlayer.WatchedAttributes.SetInt("onHurtCounter", targetPlayer.WatchedAttributes.GetInt("onHurtCounter") + 1);

            targetPlayer.WatchedAttributes.MarkPathDirty("kbdirX");
            targetPlayer.WatchedAttributes.MarkPathDirty("kbdirY");
            targetPlayer.WatchedAttributes.MarkPathDirty("kbdirZ");
            targetPlayer.WatchedAttributes.MarkPathDirty("onHurt");
            targetPlayer.WatchedAttributes.MarkPathDirty("onHurtCounter");

            return true;
        }

        private void StartHook(HookStage stage, int stageIndex, EntityPlayer target)
        {
            if (Sapi == null || entity == null || stage == null || target == null) return;

            MarkCooldownStart();
            SetAbilityActive(true);

            activeStageIndex = stageIndex;
            activeStage = stage;
            hookStartedAtMs = Sapi.World.ElapsedMilliseconds;
            lastPullLogAtMs = 0;
            targetPlayer = target;

            if (targetPlayer?.Stats != null)
            {
                float modifier = HookVictimWalkSpeedMult - 1f;
                targetPlayer.Stats.Remove("walkspeed", WalkSpeedStatCodeHook);
                targetPlayer.Stats.Set("walkspeed", WalkSpeedStatCodeHook, modifier, true);
                targetPlayer.walkSpeed = targetPlayer.Stats.GetBlended("walkspeed");
            }

            // Disable jumping while hooked
            targetPlayer.WatchedAttributes.SetBool("alegacyvsquest:canjump", false);

            UnregisterCallbackSafe(ref hookStartCallbackId);
            UnregisterGameTickListenerSafe(ref hookTickListenerId);

            BossBehaviorUtils.StopAiAndFreeze(entity);

            TryPlaySound(stage.sound, stage.soundRange, stage.soundStartMs, stage.soundVolume);
            TryPlayAnimation(stage.windupAnimation);

            int windup = Math.Max(0, stage.windupMs);
            int pullMs = Math.Max(100, stage.pullMs);
            hookEndsAtMs = hookStartedAtMs + windup + pullMs;
            pullStartsAtMs = hookStartedAtMs + windup;

            if (windup > 0)
            {
                hookStartCallbackId = Sapi.Event.RegisterCallback(_ =>
                {
                    BeginPull(stage);
                }, windup);
            }
            else
            {
                BeginPull(stage);
            }
        }

        private void BeginPull(HookStage stage)
        {
            if (Sapi == null || entity == null || stage == null) return;

            TryPlayAnimation(stage.pullAnimation);

            // Hook chain line from boss to target
            if (targetPlayer != null)
            {
                Vec3d from = entity.Pos.XYZ.Add(0, 1.5, 0);
                Vec3d to = targetPlayer.Pos.XYZ.Add(0, 1, 0);
                ParticleUtils.SpawnLine(Sapi, from, to, ParticleUtils.Colors.Chain, 10, 0.3f);
            }
        }

        private void StopHook()
        {
            UnregisterCallbackSafe(ref hookStartCallbackId);
            UnregisterGameTickListenerSafe(ref hookTickListenerId);

            if (!IsAbilityActive) return;

            SetAbilityActive(false);

            // Ensure no leftover knockback impulse.
            if (targetPlayer != null)
            {
                if (targetPlayer.Stats != null)
                {
                    targetPlayer.Stats.Remove("walkspeed", WalkSpeedStatCodeHook);
                    targetPlayer.walkSpeed = targetPlayer.Stats.GetBlended("walkspeed");
                }

                targetPlayer.WatchedAttributes.SetDouble("kbdirX", 0.0);
                targetPlayer.WatchedAttributes.SetDouble("kbdirY", 0.0);
                targetPlayer.WatchedAttributes.SetDouble("kbdirZ", 0.0);
                targetPlayer.WatchedAttributes.MarkPathDirty("kbdirX");
                targetPlayer.WatchedAttributes.MarkPathDirty("kbdirY");
                targetPlayer.WatchedAttributes.MarkPathDirty("kbdirZ");

                targetPlayer.WatchedAttributes.SetFloat("onHurt", 0f);
                targetPlayer.WatchedAttributes.MarkPathDirty("onHurt");

                // Re-enable jumping
                targetPlayer.WatchedAttributes.SetBool("alegacyvsquest:canjump", true);
            }

            targetPlayer = null;

            activeStage = null;

            hookStartedAtMs = 0;
            hookEndsAtMs = 0;
            pullStartsAtMs = 0;
            lastPullLogAtMs = 0;

            if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
            {
                var stage = stages[activeStageIndex];

                if (!string.IsNullOrWhiteSpace(stage.windupAnimation))
                {
                    entity?.AnimManager?.StopAnimation(stage.windupAnimation);
                }

                if (!string.IsNullOrWhiteSpace(stage.pullAnimation))
                {
                    entity?.AnimManager?.StopAnimation(stage.pullAnimation);
                }
            }

            activeStageIndex = -1;
        }


        private void TryPlaySound(HookStage stage)
        {
            if (Sapi == null || stage == null) return;
            TryPlaySound(stage.sound, stage.soundRange, stage.soundStartMs, stage.soundVolume);
        }
    }
}
