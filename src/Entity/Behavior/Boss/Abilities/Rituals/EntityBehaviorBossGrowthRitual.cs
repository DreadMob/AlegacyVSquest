using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossGrowthRitual : BossAbilityBase
    {
        private const string GrowthStageKey = "alegacyvsquest:bossgrowthstage";
        private const string GrowthScaleKey = "alegacyvsquest:bossgrowthritual:growthScale";

        protected override string CooldownKey => "alegacyvsquest:bossgrowthritual:lastStartMs";
        protected override bool UseHealthBasedStages() => false;
        protected override bool RequiresTarget() => false;
        protected override int CheckIntervalMs => 500;

        private const string GrowthAnimSeqKey = "alegacyvsquest:bossgrowthritual:animseq";
        private const string GrowthAnimKey = "alegacyvsquest:bossgrowthritual:anim";
        private const string GrowthAnimMsKey = "alegacyvsquest:bossgrowthritual:animms";
        private const string GrowthDamageMultKey = "alegacyvsquest:bossgrowthritual:damagemult";

        private class Stage : BossAbilityStage
        {
            public float sizeMultiplier;
            public float speedMultiplier;
            public float damageMultiplier;
            public string animation;
            public int animationMs;
            public string sound;
            public float soundRange;
            public int soundStartMs;
            public bool lightningFlash;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                sizeMultiplier = json["sizeMultiplier"].AsFloat(1f);
                speedMultiplier = json["speedMultiplier"].AsFloat(1f);
                damageMultiplier = json["damageMultiplier"].AsFloat(0f);
                animation = json["animation"].AsString(null);
                animationMs = json["animationMs"].AsInt(0);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(24f);
                soundStartMs = json["soundStartMs"].AsInt(0);
                lightningFlash = json["lightningFlash"].AsBool(false);
            }
        }

        private List<Stage> stages = new List<Stage>();

        private bool baseSizesCaptured;
        private float baseClientSize = 1f;
        private Vec2f baseCollisionBoxSize;
        private Vec2f baseDeadCollisionBoxSize;
        private Vec2f baseSelectionBoxSize;
        private Vec2f baseDeadSelectionBoxSize;
        private double baseEyeHeight;
        private float baseWalkSpeed;

        private float lastAppliedClientScale = 1f;
        private float lastAppliedServerScale = 1f;

        private int lastClientAnimSeq;

        private WeatherSystemBase weatherSystem;

        public EntityBehaviorBossGrowthRitual(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossgrowthritual";

        protected override void InitializeStages(JsonObject attributes)
        {
            if (Sapi != null)
            {
                weatherSystem = Sapi.ModLoader?.GetModSystem<WeatherSystemBase>();
            }

            CaptureBaseSizes();
            stages = ParseStages<Stage>(attributes);
        }

        protected override bool ShouldCheckAbility()
        {
            return !IsAbilityActive;
        }

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;

            MarkCooldownStart();

            entity.WatchedAttributes.SetInt(GrowthStageKey, stageIndex + 1);
            entity.WatchedAttributes.MarkPathDirty(GrowthStageKey);

            ApplyGrowth(stage);
        }

        protected override void StopAbility()
        {
            // No-op: growth is instant, no cleanup needed
        }

        protected override bool OnAbilityTick(float dt)
        {
            return false; // Instant activation, no ongoing state
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            // Client-side growth application (runs every tick on client)
            ApplyClientGrowthFromWatchedAttributes();

            // Server-side collision box update for growth
            if (Sapi == null || entity == null || stages.Count == 0) return;
            if (!entity.Alive) return;

            float currentScale = entity?.WatchedAttributes?.GetFloat(GrowthScaleKey, 1f) ?? 1f;
            if (currentScale < 0.01f) currentScale = 1f;

            if (Math.Abs(currentScale - 1f) > 0.001f && Math.Abs(currentScale - lastAppliedServerScale) > 0.0005f)
            {
                lastAppliedServerScale = currentScale;
            }

            if (Math.Abs(currentScale - 1f) > 0.001f)
            {
                var cb = entity.Properties?.CollisionBoxSize;
                if (cb != null)
                {
                    entity.SetCollisionBox(cb.X, cb.Y);
                    var sb = entity.Properties.SelectionBoxSize ?? cb;
                    entity.SetSelectionBox(sb.X, sb.Y);
                }

                double td = (entity.touchDistance = entity.GetTouchDistance());
                entity.touchDistanceSq = td * td;
            }
        }

        private void TryRestoreFullHealth()
        {
            if (!entity.TryGetHealth(out var healthTree, out float currentHealth, out float maxHealth)) return;
            if (healthTree == null || maxHealth <= 0f) return;

            if (currentHealth < maxHealth)
            {
                healthTree.SetFloat("currenthealth", maxHealth);
                entity.WatchedAttributes?.MarkPathDirty("health");
            }
        }

        private void TryPlayClientGrowthAnimationFromWatchedAttributes()
        {
            if (entity?.Api is not ICoreClientAPI capi) return;

            var wa = entity?.WatchedAttributes;
            if (wa == null) return;

            int seq = wa.GetInt(GrowthAnimSeqKey, 0);

            if (seq == 0 || seq == lastClientAnimSeq) return;
            lastClientAnimSeq = seq;

            string anim = wa.GetString(GrowthAnimKey, null);
            int ms = wa.GetInt(GrowthAnimMsKey, 0);

            if (string.IsNullOrWhiteSpace(anim)) return;

            entity?.AnimManager?.StartAnimation(anim);

            if (ms <= 0) return;

            capi.Event.RegisterCallback(_ =>
            {
                entity?.AnimManager?.StopAnimation(anim);
            }, ms);
        }

        private void TryTriggerClientAnimation(Stage stage)
        {
            if (stage == null || string.IsNullOrWhiteSpace(stage.animation)) return;

            var wa = entity?.WatchedAttributes;
            if (wa == null) return;

            wa.SetString(GrowthAnimKey, stage.animation);
            wa.SetInt(GrowthAnimMsKey, Math.Max(0, stage.animationMs));
            wa.SetInt(GrowthAnimSeqKey, wa.GetInt(GrowthAnimSeqKey, 0) + 1);
            wa.MarkPathDirty(GrowthAnimKey);
            wa.MarkPathDirty(GrowthAnimMsKey);
            wa.MarkPathDirty(GrowthAnimSeqKey);
        }


        private void ApplyGrowth(Stage stage)
        {
            bool applySize = stage.sizeMultiplier > 1.01f;
            bool applySpeed = stage.speedMultiplier > 1.01f;
            if (!applySize && !applySpeed) return;

            if (stage.damageMultiplier <= 0f)
            {
                stage.damageMultiplier = applySize ? stage.sizeMultiplier : 1f;
            }

            entity?.WatchedAttributes?.SetFloat(GrowthScaleKey, stage.sizeMultiplier);
            entity?.WatchedAttributes?.MarkPathDirty(GrowthScaleKey);

            if (applySize && entity?.Properties != null)
            {
                if (entity.Properties.Client != null)
                {
                    entity.Properties.Client.Size = baseClientSize * stage.sizeMultiplier;
                }

                if (entity.Properties.CollisionBoxSize != null)
                {
                    entity.Properties.CollisionBoxSize = new Vec2f(baseCollisionBoxSize.X * stage.sizeMultiplier, baseCollisionBoxSize.Y * stage.sizeMultiplier);
                }

                if (entity.Properties.SelectionBoxSize != null)
                {
                    entity.Properties.SelectionBoxSize = new Vec2f(baseSelectionBoxSize.X * stage.sizeMultiplier, baseSelectionBoxSize.Y * stage.sizeMultiplier);
                }

                if (entity.Properties.DeadCollisionBoxSize != null)
                {
                    entity.Properties.DeadCollisionBoxSize = new Vec2f(baseDeadCollisionBoxSize.X * stage.sizeMultiplier, baseDeadCollisionBoxSize.Y * stage.sizeMultiplier);
                }

                if (entity.Properties.DeadSelectionBoxSize != null)
                {
                    entity.Properties.DeadSelectionBoxSize = new Vec2f(baseDeadSelectionBoxSize.X * stage.sizeMultiplier, baseDeadSelectionBoxSize.Y * stage.sizeMultiplier);
                }
            }

            entity.Properties.EyeHeight = baseEyeHeight * stage.sizeMultiplier;

            var cb = entity.Properties.CollisionBoxSize;
            if (cb != null)
            {
                entity.SetCollisionBox(cb.X, cb.Y);
                var sb = entity.Properties.SelectionBoxSize ?? cb;
                entity.SetSelectionBox(sb.X, sb.Y);
            }

            double td = (entity.touchDistance = entity.GetTouchDistance());
            entity.touchDistanceSq = td * td;

            if (applySpeed && entity?.Stats != null)
            {
                if (baseWalkSpeed <= 0f)
                {
                    baseWalkSpeed = entity.Stats.GetBlended("walkspeed");
                }

                if (baseWalkSpeed > 0f)
                {
                    entity.Stats.Set("walkspeed", "alegacyvsquest", baseWalkSpeed * stage.speedMultiplier, true);
                }
            }

            if (stage.damageMultiplier > 0f && entity?.WatchedAttributes != null)
            {
                entity.WatchedAttributes.SetFloat(GrowthDamageMultKey, stage.damageMultiplier);
                entity.WatchedAttributes.MarkPathDirty(GrowthDamageMultKey);
            }

            TryRestoreFullHealth();

            TryPlayStageAnimation(stage);
            TryPlayStageSound(stage);
            TrySpawnLightningFlash(stage);
        }

        private void TrySpawnLightningFlash(Stage stage)
        {
            if (stage == null || !stage.lightningFlash) return;
            weatherSystem?.SpawnLightningFlash(entity?.Pos?.XYZ);
        }

        private void TryPlayStageAnimation(Stage stage)
        {
            if (stage == null || string.IsNullOrWhiteSpace(stage.animation)) return;

            entity?.AnimManager?.StartAnimation(stage.animation);

            int ms = stage.animationMs;
            if (ms <= 0) return;

            if (Sapi == null) return;

            Sapi.Event.RegisterCallback(_ =>
            {
                entity?.AnimManager?.StopAnimation(stage.animation);
            }, ms);

            TryTriggerClientAnimation(stage);
        }

        private void TryPlayStageSound(Stage stage)
        {
            if (stage == null || string.IsNullOrWhiteSpace(stage.sound) || Sapi == null) return;

            AssetLocation soundLoc = AssetLocation.Create(stage.sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            float range = stage.soundRange;
            if (range <= 0f) range = 24f;

            int startMs = stage.soundStartMs;
            if (startMs > 0)
            {
                Sapi.Event.RegisterCallback(_ =>
                {
                    if (entity == null || !entity.Alive) return;
                    float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                    Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, range, 1f);
                }, startMs);
                return;
            }

            if (entity == null || !entity.Alive) return;
            float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
            Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, range, 1f);
        }

        private void CaptureBaseSizes()
        {
            if (baseSizesCaptured || entity?.Properties == null) return;

            baseSizesCaptured = true;

            baseEyeHeight = entity.Properties.EyeHeight;
            if (entity.Properties.Client != null)
            {
                baseClientSize = entity.Properties.Client.Size;
            }

            if (entity.Properties.CollisionBoxSize != null)
            {
                baseCollisionBoxSize = entity.Properties.CollisionBoxSize;
            }

            if (entity.Properties.SelectionBoxSize != null)
            {
                baseSelectionBoxSize = entity.Properties.SelectionBoxSize;
            }
            else
            {
                baseSelectionBoxSize = baseCollisionBoxSize;
            }

            if (entity.Properties.DeadCollisionBoxSize != null)
            {
                baseDeadCollisionBoxSize = entity.Properties.DeadCollisionBoxSize;
            }

            if (entity.Properties.DeadSelectionBoxSize != null)
            {
                baseDeadSelectionBoxSize = entity.Properties.DeadSelectionBoxSize;
            }
            else
            {
                baseDeadSelectionBoxSize = baseDeadCollisionBoxSize;
            }
        }

        private void ApplyClientGrowthFromWatchedAttributes()
        {
            if (!(entity?.Api is ICoreClientAPI)) return;

            TryPlayClientGrowthAnimationFromWatchedAttributes();

            CaptureBaseSizes();

            float scale = 1f;
            var wa = entity?.WatchedAttributes;
            if (wa != null)
            {
                scale = wa.GetFloat(GrowthScaleKey, 1f);
            }

            if (scale < 0.01f) scale = 1f;
            if (Math.Abs(scale - lastAppliedClientScale) < 0.001f) return;
            lastAppliedClientScale = scale;

            if (entity?.Properties?.Client != null)
            {
                entity.Properties.Client.Size = baseClientSize * scale;
            }

            if (entity?.Properties?.CollisionBoxSize != null)
            {
                entity.Properties.CollisionBoxSize = new Vec2f(baseCollisionBoxSize.X * scale, baseCollisionBoxSize.Y * scale);
            }

            if (entity?.Properties?.SelectionBoxSize != null)
            {
                entity.Properties.SelectionBoxSize = new Vec2f(baseSelectionBoxSize.X * scale, baseSelectionBoxSize.Y * scale);
            }

            if (entity?.Properties?.DeadCollisionBoxSize != null)
            {
                entity.Properties.DeadCollisionBoxSize = new Vec2f(baseDeadCollisionBoxSize.X * scale, baseDeadCollisionBoxSize.Y * scale);
            }

            if (entity?.Properties?.DeadSelectionBoxSize != null)
            {
                entity.Properties.DeadSelectionBoxSize = new Vec2f(baseDeadSelectionBoxSize.X * scale, baseDeadSelectionBoxSize.Y * scale);
            }

            if (entity?.Properties != null)
            {
                entity.Properties.EyeHeight = baseEyeHeight * scale;
            }

            var cb = entity?.Properties?.CollisionBoxSize;
            if (cb != null)
            {
                entity.SetCollisionBox(cb.X, cb.Y);
                var sb = entity.Properties.SelectionBoxSize ?? cb;
                entity.SetSelectionBox(sb.X, sb.Y);
            }

            double td = (entity.touchDistance = entity.GetTouchDistance());
            entity.touchDistanceSq = td * td;
        }

        // Required abstract overrides for BossAbilityBase (event-driven mode)
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => index >= 0 && index < stages.Count ? stages[index] : null;
        protected override float GetStageHealthThreshold(object stage) => stage is Stage s ? s.whenHealthRelBelow : 1f;
        protected override float GetStageCooldown(object stage) => stage is Stage s ? s.cooldownSeconds : 0f;
        protected override float GetMaxTargetRange(object stage) => 0f;
    }
}
