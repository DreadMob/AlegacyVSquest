using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossRandomLightning : BossAbilityBase
    {
        private const string LastStrikeStartKeyPrefix = "alegacyvsquest:bossrandomlightning:lastStartMs:";

        protected override string CooldownKey => "alegacyvsquest:bossrandomlightning:lastStartMs";

        private class LightningStage : BossAbilityStage
        {
            public int minCount;
            public int maxCount;
            public float chance;
            public float minRadius;
            public float maxRadius;
            public int warningDelayMs;
            public string warningSound;
            public float warningSoundRange;
            public float warningSoundVolume;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                minCount = json["minCount"].AsInt(3);
                maxCount = json["maxCount"].AsInt(6);
                chance = json["chance"].AsFloat(1f);
                minRadius = json["minRadius"].AsFloat(0f);
                maxRadius = json["maxRadius"].AsFloat(12f);
                warningDelayMs = json["warningDelayMs"].AsInt(1200);
                warningSound = json["warningSound"].AsString("effect/lightning");
                warningSoundRange = json["warningSoundRange"].AsFloat(24f);
                warningSoundVolume = json["warningSoundVolume"].AsFloat(1f);

                // Validation
                if (minCount < 1) minCount = 1;
                if (maxCount < minCount) maxCount = minCount;
                if (minRadius < 0f) minRadius = 0f;
                if (maxRadius < minRadius) maxRadius = minRadius;
                if (warningDelayMs < 0) warningDelayMs = 0;
            }
        }

        private List<LightningStage> stages = new List<LightningStage>();
        private long callbackId;
        private WeatherSystemBase weatherSystem;

        public EntityBehaviorBossRandomLightning(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossrandomlightning";

        protected override void InitializeStages(JsonObject attributes)
        {
            weatherSystem = entity.Api.ModLoader.GetModSystem<WeatherSystemBase>();
            stages = ParseStages<LightningStage>(attributes);
        }

        protected override int GetStageCount() => stages.Count;

        protected override object GetStage(int index) => stages[index];

        protected override float GetStageHealthThreshold(object stage) => ((LightningStage)stage).whenHealthRelBelow;

        protected override float GetStageCooldown(object stage) => ((LightningStage)stage).cooldownSeconds;

        protected override float GetMaxTargetRange(object stage) => ((LightningStage)stage).maxTargetRange;

        protected override bool ShouldCheckAbility() => !IsAbilityActive;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not LightningStage stage) return;
            
            if (stage.chance < 1f && Sapi.World.Rand.NextDouble() > stage.chance)
            {
                MarkCooldownStart();
                return;
            }
            
            MarkCooldownStart();
            TriggerLightning(stage);
        }

        protected override void StopAbility()
        {
            UnregisterCallbackSafe(ref callbackId);
            SetAbilityActive(false);
        }

        protected override bool OnAbilityTick(float dt) => false;

        private void TriggerLightning(LightningStage stage)
        {
            if (stage == null) return;

            int minCount = Math.Max(1, stage.minCount);
            int maxCount = Math.Max(minCount, stage.maxCount);
            int count = minCount;
            if (maxCount > minCount)
            {
                count = minCount + (Sapi?.World?.Rand?.Next(maxCount - minCount + 1) ?? 0);
            }

            for (int i = 0; i < count; i++)
            {
                Vec3d strikePos = GetRandomStrikePosition(stage);
                TriggerStrike(stage, strikePos);
            }
        }

        private Vec3d GetRandomStrikePosition(LightningStage stage)
        {
            double minRadius = Math.Max(0, stage.minRadius);
            double maxRadius = Math.Max(minRadius + 0.1, stage.maxRadius);

            double angle = Sapi.World.Rand.NextDouble() * Math.PI * 2.0;
            double dist = minRadius + Sapi.World.Rand.NextDouble() * (maxRadius - minRadius);

            double x = entity.Pos.X + Math.Cos(angle) * dist;
            double z = entity.Pos.Z + Math.Sin(angle) * dist;
            int dim = entity.Pos.Dimension;
            double y = entity.Pos.Y + dim * 32768.0;

            return new Vec3d(x, y, z);
        }

        private void TriggerStrike(LightningStage stage, Vec3d strikePos)
        {
            if (Sapi == null || stage == null || strikePos == null) return;

            int delayMs = Math.Max(0, stage.warningDelayMs);

            // Warning telegraph - bright ring + pillar particles on ground before strike
            ParticleUtils.SpawnAuraRing(Sapi, strikePos, 2.5f, ParticleUtils.Colors.LightningBlue, 16, 0.6f);

            // Additional vertical pillar particles for visibility
            for (int p = 0; p < 6; p++)
            {
                Vec3d pillarPos = strikePos.AddCopy(0, p * 0.5, 0);
                Sapi.World.SpawnParticles(new SimpleParticleProperties(
                    2, 4, ColorUtil.ToRgba(220, 100, 180, 255),
                    pillarPos.AddCopy(-0.2, 0, -0.2), pillarPos.AddCopy(0.2, 0.3, 0.2),
                    new Vec3f(0, 0.3f, 0), new Vec3f(0, 0.6f, 0),
                    1.0f, -0.01f, 0.4f, 0.8f, EnumParticleModel.Quad
                ));
            }

            // Loop warning sound until the strike happens.
            // This gives players a continuous cue without relying on stacking/overlapping sounds.
            if (!string.IsNullOrWhiteSpace(stage.warningSound))
            {
                int intervalMs = 600;
                if (intervalMs < 250) intervalMs = 250;

                // Cap the amount of scheduled warning plays to avoid excessive callbacks.
                int maxPlays = 10;
                int plays = 1;
                if (delayMs > 0)
                {
                    plays = 1 + (delayMs / intervalMs);
                }
                if (plays > maxPlays) plays = maxPlays;

                for (int i = 0; i < plays; i++)
                {
                    int playDelay = i * intervalMs;
                    if (playDelay > delayMs) break;

                    Sapi.Event.RegisterCallback(_ =>
                    {
                        TryPlayWarningSound(stage, strikePos);
                    }, playDelay);
                }
            }

            Sapi.Event.RegisterCallback(_ =>
            {
                weatherSystem?.SpawnLightningFlash(strikePos);
            }, delayMs);
        }

        private void TryPlayWarningSound(LightningStage stage, Vec3d strikePos)
        {
            if (string.IsNullOrWhiteSpace(stage.warningSound)) return;

            AssetLocation soundLoc = AssetLocation.Create(stage.warningSound, "game").WithPathPrefixOnce("sounds/");
            float volume = stage.warningSoundVolume;
            if (volume <= 0f) volume = 1f;

            // Warning should be audible near the strike position.
            float range = stage.warningSoundRange > 0f ? stage.warningSoundRange : 32f;

            float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
            Sapi.World.PlaySoundAt(soundLoc, strikePos.X, strikePos.Y, strikePos.Z, null, pitch, volume);
        }
    }
}
