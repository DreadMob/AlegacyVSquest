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
    public class EntityBehaviorBossPeriodicSpawn : BossAbilityBase
    {
        private const string SummonedByEntityIdKey = "alegacyvsquest:bosssummonritual:summonedByEntityId";
        private const string SummonedByEntityCodeKey = "alegacyvsquest:bosssummonritual:summonedByEntityCode";

        protected override string CooldownKey => "alegacyvsquest:bossperiodicspawn:lastStartMs";
        protected override bool UsePeriodicTick() => true;
        protected override int CheckIntervalMs => 500;

        private class Stage : BossAbilityStage
        {
            public string entityCode;
            public int minCount;
            public int maxCount;
            public float chance;

            public int maxNearby;
            public float nearbyRange;

            public float spawnRange;

            public bool requireHasTarget;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                entityCode = json["entityCode"].AsString(null);
                minCount = json["minCount"].AsInt(1);
                maxCount = json["maxCount"].AsInt(1);
                chance = json["chance"].AsFloat(1f);
                maxNearby = json["maxNearby"].AsInt(0);
                nearbyRange = json["nearbyRange"].AsFloat(0f);
                spawnRange = json["spawnRange"].AsFloat(8f);
                requireHasTarget = json["requireHasTarget"].AsBool(true);

                if (spawnRange <= 0f) spawnRange = 4f;
            }
        }

        private List<Stage> stages = new List<Stage>();

        public EntityBehaviorBossPeriodicSpawn(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossperiodicspawn";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        protected override void OnPeriodicTick(float dt)
        {
            if (Sapi == null || entity == null) return;
            if (stages.Count == 0) return;
            if (entity.Api?.Side != EnumAppSide.Server) return;

            if (!entity.Alive) return;

            if (!TryGetHealthFraction(out float frac)) return;

            (object stageObj, int stageIndex) = FindStageForHealth(frac);
            if (stageObj == null || stageIndex < 0) return;

            var stage = stageObj as Stage;
            if (stage == null) return;

            if (!IsCooldownReady(stage)) return;

            Entity target = null;
            float dist = 0f;
            if (!TryFindTarget(stage, out target, out dist))
            {
                if (stage.requireHasTarget) return;
            }
            else
            {
                if (dist < stage.minTargetRange) return;
                if (dist > stage.maxTargetRange) return;
            }

            if (stage.chance < 1f && Sapi.World.Rand.NextDouble() > stage.chance) return;

            if (stage.maxNearby > 0)
            {
                float range = stage.nearbyRange > 0f ? stage.nearbyRange : Math.Max(1f, stage.spawnRange);
                int aliveNearby = CountAliveNearby(stage.entityCode, range);
                if (aliveNearby >= stage.maxNearby) return;
            }

            MarkCooldownStart();

            int count = stage.minCount;
            if (stage.maxCount > stage.minCount)
            {
                count = stage.minCount + Sapi.World.Rand.Next(stage.maxCount - stage.minCount + 1);
            }

            Spawn(stage, count);
        }

        private bool TryFindTarget(Stage stage, out Entity target, out float dist)
        {
            target = null;
            dist = 0f;

            if (Sapi == null || entity == null || stage == null) return false;

            var own = entity.Pos.XYZ;
            float range = Math.Max(4f, stage.maxTargetRange > 0f ? stage.maxTargetRange : 40f);
            var found = Sapi.World.GetNearestEntity(own, range, range, e => e is EntityPlayer);
            if (found == null || !found.Alive) return false;
            if (found.Pos.Dimension != entity.Pos.Dimension) return false;

            target = found;
            dist = (float)found.Pos.DistanceTo(entity.Pos);
            return true;
        }

        private int CountAliveNearby(string entityCode, float range)
        {
            if (Sapi == null || entity == null || string.IsNullOrWhiteSpace(entityCode) || range <= 0f) return 0;

            int dim = entity.Pos.Dimension;
            var center = new Vec3d(entity.Pos.X, entity.Pos.Y + dim * 32768.0, entity.Pos.Z);
            var entities = Sapi.World.GetEntitiesAround(center, range, range, e => e != null && e.Alive);
            if (entities == null) return 0;

            int alive = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                var code = e?.Code?.ToString();
                if (string.IsNullOrWhiteSpace(code)) continue;

                if (string.Equals(code, entityCode, StringComparison.OrdinalIgnoreCase))
                {
                    alive++;
                }
            }

            return alive;
        }

        private void Spawn(Stage stage, int count)
        {
            if (Sapi == null || entity == null || stage == null || string.IsNullOrWhiteSpace(stage.entityCode) || count <= 0) return;

            AssetLocation codeLoc = new AssetLocation(stage.entityCode);
            EntityProperties type = Sapi.World.GetEntityType(codeLoc);

            if (type == null && string.Equals(codeLoc.Domain, "game", StringComparison.OrdinalIgnoreCase))
            {
                type = Sapi.World.GetEntityType(new AssetLocation("survival", codeLoc.Path));
            }

            if (type == null) return;

            int dim = entity.Pos.Dimension;
            for (int i = 0; i < count; i++)
            {
                double angle = Sapi.World.Rand.NextDouble() * Math.PI * 2.0;
                double dist = stage.spawnRange * (0.5 + Sapi.World.Rand.NextDouble() * 0.5);
                double x = entity.Pos.X + Math.Cos(angle) * dist;
                double z = entity.Pos.Z + Math.Sin(angle) * dist;
                double y = entity.Pos.Y;

                float yaw = (float)(Sapi.World.Rand.NextDouble() * Math.PI * 2.0);
                var spawnPos = new Vec3d(x, y + dim * 32768.0, z);

                Entity spawned = Sapi.World.ClassRegistry.CreateEntity(type);
                if (spawned == null) continue;

                if (entity.EntityId != 0)
                {
                    spawned.WatchedAttributes.SetLong(SummonedByEntityIdKey, entity.EntityId);
                    spawned.WatchedAttributes.MarkPathDirty(SummonedByEntityIdKey);
                }

                var summonerCode = entity?.Code?.ToString();
                if (!string.IsNullOrWhiteSpace(summonerCode))
                {
                    spawned.WatchedAttributes.SetString(SummonedByEntityCodeKey, summonerCode);
                    spawned.WatchedAttributes.MarkPathDirty(SummonedByEntityCodeKey);
                }

                spawned.Pos.SetPosWithDimension(spawnPos);
                spawned.Pos.SetFrom(spawned.Pos);
                spawned.Pos.Yaw = yaw;

                Sapi.World.SpawnEntity(spawned);

                TryDisableFleeForSummonedWolves(spawned);
            }
        }

        private void TryDisableFleeForSummonedWolves(Entity spawned)
        {
            if (Sapi == null || spawned == null) return;

            string code = spawned.Code?.ToShortString();
            if (string.IsNullOrWhiteSpace(code)) return;
            if (!code.Contains("wolf", StringComparison.OrdinalIgnoreCase)) return;

            long summonerId = spawned.WatchedAttributes?.GetLong(SummonedByEntityIdKey, 0) ?? 0;
            if (summonerId <= 0) return;

            Sapi.Event.RegisterCallback(_ =>
            {
                if (spawned == null || !spawned.Alive) return;

                var taskAi = spawned.GetBehavior<EntityBehaviorTaskAI>();
                if (taskAi?.TaskManager == null) return;

                taskAi.TaskManager.StopTasks();

                var tasks = taskAi.TaskManager.AllTasks;
                for (int i = tasks.Count - 1; i >= 0; i--)
                {
                    var t = tasks[i];
                    var tn = t?.GetType()?.Name ?? "";
                    if (tn.IndexOf("AiTaskFlee", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        taskAi.TaskManager.RemoveTask(t);
                    }
                }
            }, 1);
        }

        // Required abstract overrides for BossAbilityBase (not used in periodic tick mode)
        protected override void ActivateAbility(object stage, int stageIndex, EntityPlayer target) { }
        protected override void StopAbility() { }
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => index >= 0 && index < stages.Count ? stages[index] : null;
        protected override float GetStageHealthThreshold(object stage) => stage is Stage s ? s.whenHealthRelBelow : 1f;
        protected override float GetStageCooldown(object stage) => stage is Stage s ? s.cooldownSeconds : 0f;
        protected override float GetMaxTargetRange(object stage) => stage is Stage s ? s.maxTargetRange : 40f;
    }
}
