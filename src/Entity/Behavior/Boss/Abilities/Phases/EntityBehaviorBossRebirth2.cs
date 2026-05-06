using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossRebirth2 : BossAbilityBase
    {
        private const string AnchorKeyPrefix = "alegacyvsquest:spawner:";
        private const string TargetIdKey = "alegacyvsquest:killaction:targetid";

        private class Stage : BossAbilityStage
        {
            public string nextEntityCode;
            public bool isFinalStage;
            public int spawnDelayMs;
            public bool spawnLightning;

            public string sound;
            public float soundRange;
            public int soundStartMs;

            public string spawnSound;
            public float spawnSoundRange;
            public int spawnSoundStartMs;

            public string loopSound;
            public float loopSoundRange;
            public int loopSoundIntervalMs;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                nextEntityCode = json["nextEntityCode"].AsString(null);
                isFinalStage = json["isFinalStage"].AsBool(false);
                spawnDelayMs = json["spawnDelayMs"].AsInt(2000);
                spawnLightning = json["spawnLightning"].AsBool(true);

                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(24f);
                soundStartMs = json["soundStartMs"].AsInt(0);

                spawnSound = json["spawnSound"].AsString(null);
                spawnSoundRange = json["spawnSoundRange"].AsFloat(24f);
                spawnSoundStartMs = json["spawnSoundStartMs"].AsInt(0);

                loopSound = json["loopSound"].AsString(null);
                loopSoundRange = json["loopSoundRange"].AsFloat(24f);
                loopSoundIntervalMs = json["loopSoundIntervalMs"].AsInt(900);
            }
        }

        private List<Stage> stages = new List<Stage>();
        protected override string CooldownKey => "alegacyvsquest:bossrebirth2:lastStartMs";
        protected override bool UseHealthBasedStages() => false;
        protected override bool RequiresTarget() => false;
        protected override bool ShouldCheckAbility() => false; // Only activates on death

        private bool phaseTriggered;
        private readonly BossBehaviorUtils.LoopSound loopSoundPlayer = new BossBehaviorUtils.LoopSound();
        private WeatherSystemBase weatherSystem;

        public bool IsFinalStage => stages.Count > 0 && stages[0].isFinalStage;

        public EntityBehaviorBossRebirth2(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossrebirth2";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
            weatherSystem = Sapi?.ModLoader?.GetModSystem<WeatherSystemBase>();
        }

        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => 0f;

        protected override void ActivateAbility(object stage, int stageIndex, EntityPlayer target) { }
        protected override void StopAbility()
        {
            StopLoopSound();
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            if (Sapi != null && entity != null)
            {
                entity.WatchedAttributes.SetBool("showHealthbar", false);
                entity.WatchedAttributes.MarkPathDirty("showHealthbar");

                if (entity is EntityAgent agent)
                {
                    agent.AllowDespawn = false;
                }
            }

            base.OnEntityDeath(damageSourceForDeath);

            if (Sapi == null || entity == null || stages.Count == 0 || phaseTriggered) return;
            var stage = stages[0];
            if (stage.isFinalStage || string.IsNullOrWhiteSpace(stage.nextEntityCode)) return;

            phaseTriggered = true;
            TriggerNextPhase(stage);
        }

        private void TriggerNextPhase(Stage stage)
        {
            Vec3d pos = new Vec3d(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
            int dim = entity.Pos.Dimension;
            float yaw = entity.Pos.Yaw;

            TryPlaySound(stage.sound, stage.soundRange, stage.soundStartMs, 1f);
            StartLoopSound(stage);

            int delay = Math.Max(0, stage.spawnDelayMs);
            if (delay > 0)
            {
                Sapi.Event.RegisterCallback(_ =>
                {
                    StopLoopSound();
                    TrySpawnNextPhase(stage, pos, dim, yaw);
                }, delay);
            }
            else
            {
                StopLoopSound();
                TrySpawnNextPhase(stage, pos, dim, yaw);
            }
        }

        private void StartLoopSound(Stage stage)
        {
            if (Sapi == null || stage == null) return;
            loopSoundPlayer.Start(Sapi, entity, stage.loopSound, stage.loopSoundRange, stage.loopSoundIntervalMs);
        }

        private void StopLoopSound()
        {
            loopSoundPlayer.Stop();
        }

        private void TrySpawnNextPhase(Stage stage, Vec3d pos, int dim, float yaw)
        {
            if (stage.spawnLightning)
            {
                weatherSystem?.SpawnLightningFlash(pos);
            }

            var type = Sapi.World.GetEntityType(new AssetLocation(stage.nextEntityCode));
            if (type == null) return;

            Entity newEntity = Sapi.World.ClassRegistry.CreateEntity(type);
            if (newEntity == null) return;

            CopyTargetId(newEntity);
            CopyAnchor(newEntity);

            double nowHours = Sapi.World?.Calendar?.TotalHours ?? 0;
            newEntity.WatchedAttributes?.SetDouble("alegacyvsquest:bosshunt:spawnedAtTotalHours", nowHours);
            newEntity.WatchedAttributes?.MarkPathDirty("alegacyvsquest:bosshunt:spawnedAtTotalHours");

            newEntity.Pos.SetPosWithDimension(new Vec3d(pos.X, pos.Y + dim * 32768.0, pos.Z));
            newEntity.Pos.Yaw = yaw;
            newEntity.Pos.SetFrom(newEntity.Pos);

            Sapi.World.SpawnEntity(newEntity);

            TryPlaySound(stage.spawnSound, stage.spawnSoundRange, stage.spawnSoundStartMs, 1f);

            if (entity != null)
            {
                if (entity is EntityAgent agent) agent.AllowDespawn = true;
                Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            }
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopLoopSound();
            base.OnEntityDespawn(despawn);
        }

        private void CopyTargetId(Entity newEntity)
        {
            string targetId = entity?.WatchedAttributes?.GetString(TargetIdKey, null);
            if (string.IsNullOrWhiteSpace(targetId) || newEntity?.WatchedAttributes == null) return;

            newEntity.WatchedAttributes.SetString(TargetIdKey, targetId);
            newEntity.WatchedAttributes.MarkPathDirty(TargetIdKey);
        }

        private void CopyAnchor(Entity newEntity)
        {
            if (newEntity?.WatchedAttributes == null || entity?.WatchedAttributes == null) return;

            int dim = entity.WatchedAttributes.GetInt(AnchorKeyPrefix + "dim", int.MinValue);
            int x = entity.WatchedAttributes.GetInt(AnchorKeyPrefix + "x", int.MinValue);
            int y = entity.WatchedAttributes.GetInt(AnchorKeyPrefix + "y", int.MinValue);
            int z = entity.WatchedAttributes.GetInt(AnchorKeyPrefix + "z", int.MinValue);

            if (dim == int.MinValue || x == int.MinValue || y == int.MinValue || z == int.MinValue) return;

            newEntity.WatchedAttributes.SetInt(AnchorKeyPrefix + "x", x);
            newEntity.WatchedAttributes.SetInt(AnchorKeyPrefix + "y", y);
            newEntity.WatchedAttributes.SetInt(AnchorKeyPrefix + "z", z);
            newEntity.WatchedAttributes.SetInt(AnchorKeyPrefix + "dim", dim);

            newEntity.WatchedAttributes.MarkPathDirty(AnchorKeyPrefix + "x");
            newEntity.WatchedAttributes.MarkPathDirty(AnchorKeyPrefix + "y");
            newEntity.WatchedAttributes.MarkPathDirty(AnchorKeyPrefix + "z");
            newEntity.WatchedAttributes.MarkPathDirty(AnchorKeyPrefix + "dim");
        }
    }
}
