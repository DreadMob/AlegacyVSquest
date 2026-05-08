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
    public class EntityBehaviorBossRespawn : BossAbilityBase
    {
        private const string AttrRespawnAtHours = "alegacyvsquest:bossrespawnAtTotalHours";
        private const string AttrRespawnX = "alegacyvsquest:bossrespawnX";
        private const string AttrRespawnY = "alegacyvsquest:bossrespawnY";
        private const string AttrRespawnZ = "alegacyvsquest:bossrespawnZ";
        private const string AttrRespawnDim = "alegacyvsquest:bossrespawnDim";

        private class Stage : BossAbilityStage
        {
            public double respawnInGameHours;
            public bool spawnNewBoss;
            public string respawnEntityCode;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                respawnInGameHours = json["respawnInGameHours"].AsDouble(24);
                spawnNewBoss = json["spawnNewBoss"].AsBool(false);
                respawnEntityCode = json["respawnEntityCode"].AsString(null);

                if (respawnInGameHours < 0) respawnInGameHours = 24;
            }
        }

        private List<Stage> stages = new List<Stage>();
        protected override string CooldownKey => "alegacyvsquest:bossrespawn:lastCheckMs";
        protected override bool UsePeriodicTick() => true;
        protected override int CheckIntervalMs => 1000;
        protected override bool UseHealthBasedStages() => false;
        protected override bool RequiresTarget() => false;

        private bool scheduled;

        public EntityBehaviorBossRespawn(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossrespawn";

        protected override void InitializeStages(JsonObject attributes)
        {
            // Check if stages array exists first
            var stagesArray = attributes["stages"]?.AsArray();
            if (stagesArray != null && stagesArray.Length > 0)
            {
                stages = ParseStages<Stage>(attributes);
            }
            else
            {
                // No stages array - read direct properties (legacy format)
                var stage = new Stage();
                stage.respawnInGameHours = attributes["respawnInGameHours"].AsDouble(24);
                stage.spawnNewBoss = attributes["spawnNewBoss"].AsBool(false);
                stage.respawnEntityCode = attributes["respawnEntityCode"].AsString(null);
                stages.Add(stage);
            }
        }

        protected override void OnPeriodicTick(float dt)
        {
            if (Sapi == null || entity == null || stages.Count == 0) return;

            if (entity.Alive)
            {
                scheduled = false;
                return;
            }

            var stage = stages[0];

            if (!scheduled)
            {
                ScheduleRespawn(stage);
            }

            // Safety: if a new boss of the same type already exists (spawned by BossHuntSystem),
            // despawn this corpse immediately so it doesn't linger.
            if (BossAlreadyExists(stage))
            {
                Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Expire });
                return;
            }

            double respawnAt = entity.WatchedAttributes.GetDouble(AttrRespawnAtHours, double.NaN);
            if (double.IsNaN(respawnAt)) return;

            if (Sapi.World.Calendar.TotalHours < respawnAt) return;

            if (stage.spawnNewBoss)
            {
                TryRespawnNow(stage);
                return;
            }

            // Default: remove corpse after timer expires
            Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Expire });
        }

        private bool BossAlreadyExists(Stage stage)
        {
            string bossCode = string.IsNullOrWhiteSpace(stage.respawnEntityCode)
                ? entity.Code?.ToShortString()
                : stage.respawnEntityCode;

            if (string.IsNullOrWhiteSpace(bossCode) || Sapi == null || entity?.Pos == null) return false;

            var nearby = Sapi.World.GetEntitiesAround(entity.Pos.XYZ, 80f, 80f, e =>
                e != null && e.Alive && e.EntityId != entity.EntityId &&
                string.Equals(e.Code?.ToShortString(), bossCode, StringComparison.OrdinalIgnoreCase));

            return nearby != null && nearby.Length > 0;
        }

        private void ScheduleRespawn(Stage stage)
        {
            if (Sapi == null || entity == null) return;

            double respawnAt = entity.WatchedAttributes.GetDouble(AttrRespawnAtHours, double.NaN);
            if (double.IsNaN(respawnAt))
            {
                if (stage.respawnInGameHours == 0)
                {
                    respawnAt = Sapi.World.Calendar.TotalHours;
                }
                else
                {
                    respawnAt = Sapi.World.Calendar.TotalHours + stage.respawnInGameHours;
                }
                entity.WatchedAttributes.SetDouble(AttrRespawnAtHours, respawnAt);

                entity.WatchedAttributes.SetDouble(AttrRespawnX, entity.Pos.X);
                entity.WatchedAttributes.SetDouble(AttrRespawnY, entity.Pos.Y);
                entity.WatchedAttributes.SetDouble(AttrRespawnZ, entity.Pos.Z);
                entity.WatchedAttributes.SetInt(AttrRespawnDim, entity.Pos.Dimension);

                entity.WatchedAttributes.MarkPathDirty(AttrRespawnAtHours);
                entity.WatchedAttributes.MarkPathDirty(AttrRespawnX);
                entity.WatchedAttributes.MarkPathDirty(AttrRespawnY);
                entity.WatchedAttributes.MarkPathDirty(AttrRespawnZ);
                entity.WatchedAttributes.MarkPathDirty(AttrRespawnDim);
            }

            scheduled = true;
        }

        private void TryRespawnNow(Stage stage)
        {
            if (Sapi == null || entity == null) return;

            string code = string.IsNullOrWhiteSpace(stage.respawnEntityCode)
                ? entity.Code?.ToShortString()
                : stage.respawnEntityCode;
            if (string.IsNullOrWhiteSpace(code)) return;

            var type = Sapi.World.GetEntityType(new AssetLocation(code));
            if (type == null) return;

            double x = entity.WatchedAttributes.GetDouble(AttrRespawnX, entity.Pos.X);
            double y = entity.WatchedAttributes.GetDouble(AttrRespawnY, entity.Pos.Y);
            double z = entity.WatchedAttributes.GetDouble(AttrRespawnZ, entity.Pos.Z);
            int dim = entity.WatchedAttributes.GetInt(AttrRespawnDim, entity.Pos.Dimension);

            float yaw = entity.Pos.Yaw;

            Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Expire });

            Entity newEntity = Sapi.World.ClassRegistry.CreateEntity(type);
            if (newEntity == null) return;

            // EntityPos.SetPosWithDimension(Vec3d) expects Y to include dimension offset (dim*32768)
            newEntity.Pos.SetPosWithDimension(new Vec3d(x, y + dim * 32768.0, z));
            newEntity.Pos.Yaw = yaw;
            newEntity.Pos.SetFrom(newEntity.Pos);

            Sapi.World.SpawnEntity(newEntity);

            // Notify BossHuntSystem so it does not try to spawn another copy
            try
            {
                string targetId = entity?.WatchedAttributes?.GetString("alegacyvsquest:killaction:targetid", null);
                if (!string.IsNullOrWhiteSpace(targetId))
                {
                    var bh = Sapi.ModLoader?.GetModSystem<BossHuntSystem>();
                    bh?.OnBossRespawnedByAbility(targetId, newEntity);
                }
            }
            catch
            {
            }
        }

        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => 0f;

        protected override void ActivateAbility(object stage, int stageIndex, EntityPlayer target) { }
        protected override void StopAbility() { }
    }
}
