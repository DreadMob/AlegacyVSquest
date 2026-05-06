using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossHasTargetSync : EntityBehavior
    {
        private long lastCheckMs;
        private int checkIntervalMs = 100;

        public EntityBehaviorBossHasTargetSync(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosshastargetsync";

        public override void Initialize(EntityProperties properties, Vintagestory.API.Datastructures.JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            checkIntervalMs = attributes?["checkIntervalMs"].AsInt(100) ?? 100;
            if (checkIntervalMs < 50) checkIntervalMs = 50;
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            if (entity?.Api == null) return;
            if (entity.Api.Side != EnumAppSide.Server) return;
            if (!entity.Alive) return;

            long nowMs = entity.World.ElapsedMilliseconds;
            if (nowMs - lastCheckMs < checkIntervalMs) return;
            lastCheckMs = nowMs;

            bool hasTarget = false;

            try
            {
                var taskAi = entity.GetBehavior<EntityBehaviorTaskAI>();
                var tasks = taskAi?.TaskManager?.ActiveTasksBySlot;
                if (tasks != null)
                {
                    foreach (var task in tasks)
                    {
                        if (task is AiTaskBaseTargetable targetable)
                        {
                            var te = targetable.TargetEntity;
                            if (te != null && te.Alive && te.Pos != null && te.Pos.Dimension == entity.Pos.Dimension)
                            {
                                hasTarget = true;
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
                hasTarget = false;
            }

            entity.SetWatchedBoolDirty(BossBehaviorUtils.HasTargetKey, hasTarget);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            try
            {
                var wa = entity?.WatchedAttributes;
                if (wa != null)
                {
                    entity.SetWatchedBoolDirty(BossBehaviorUtils.HasTargetKey, false);
                }
            }
            catch
            {
            }

            base.OnEntityDespawn(despawn);
        }
    }
}
