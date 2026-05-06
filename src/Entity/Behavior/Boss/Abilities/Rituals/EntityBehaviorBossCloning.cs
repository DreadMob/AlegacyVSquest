using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossCloning : BossAbilityBase
    {
        private const string CloneStageKey = "alegacyvsquest:bossclonestage";
        private const string CloneOwnerIdKey = "alegacyvsquest:bossclone:ownerid";

        private const string TargetIdKey = "alegacyvsquest:killaction:targetid";
        private const string AnchorKeyPrefix = "alegacyvsquest:spawner:";

        protected override string CooldownKey => "alegacyvsquest:bossclone:lastStartMs";
        protected override bool UseHealthBasedStages() => false;
        protected override bool RequiresTarget() => false;
        protected override int CheckIntervalMs => 500;

        private class Stage : BossAbilityStage
        {
            public int cloneCount;
            public int durationMs;
            public float spawnRange;
            public float cloneDamageMult;
            public float cloneWalkSpeedMult;
            public bool cloneInvulnerable;
            public bool cloneFollowOwner;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                cloneCount = json["cloneCount"].AsInt(2);
                durationMs = json["durationMs"].AsInt(12000);
                spawnRange = json["spawnRange"].AsFloat(6f);
                cloneDamageMult = json["cloneDamageMult"].AsFloat(0.35f);
                cloneWalkSpeedMult = json["cloneWalkSpeedMult"].AsFloat(0.85f);
                cloneInvulnerable = json["cloneInvulnerable"].AsBool(true);
                cloneFollowOwner = json["cloneFollowOwner"].AsBool(false);
            }
        }

        private List<Stage> stages = new List<Stage>();

        private long cloningEndsAtMs;
        private readonly List<long> activeCloneEntityIds = new List<long>();

        public EntityBehaviorBossCloning(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosscloning";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        protected override bool ShouldCheckAbility()
        {
            return !IsAbilityActive && !IsCloneEntity();
        }

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;

            MarkCooldownStart();

            entity.WatchedAttributes.SetInt(CloneStageKey, stageIndex + 1);
            entity.WatchedAttributes.MarkPathDirty(CloneStageKey);

            StartCloning(stage, stageIndex);
        }

        protected override void StopAbility()
        {
            StopCloning();
        }

        protected override bool OnAbilityTick(float dt)
        {
            if (!IsAbilityActive) return false;

            if (Sapi.World.ElapsedMilliseconds >= cloningEndsAtMs)
            {
                StopCloning();
                return false;
            }
            return true;
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            // Clone entity special handling (not part of ability activation cycle)
            if (IsCloneEntity())
            {
                DespawnIfOwnerMissing();
                return;
            }
        }

        private void StartCloning(Stage stage, int index)
        {
            if (IsAbilityActive) return;

            SetAbilityActive(true);
            cloningEndsAtMs = Sapi.World.ElapsedMilliseconds + Math.Max(500, stage.durationMs);

            CleanupClones();
            SpawnClones(stage);
        }

        private void StopCloning()
        {
            if (!IsAbilityActive) return;

            SetAbilityActive(false);

            if (Sapi == null) return;

            for (int i = 0; i < activeCloneEntityIds.Count; i++)
            {
                long id = activeCloneEntityIds[i];
                if (id <= 0) continue;

                var e = Sapi.World.GetEntityById(id);
                if (e != null)
                {
                    Sapi.World.DespawnEntity(e, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                }
            }

            activeCloneEntityIds.Clear();
        }

        private void SpawnClones(Stage stage)
        {
            if (Sapi == null || entity == null) return;

            string code = entity.Code?.ToShortString();
            if (string.IsNullOrWhiteSpace(code)) return;

            var type = Sapi.World.GetEntityType(new AssetLocation(code));
            if (type == null) return;

            Vec3d basePos = new Vec3d(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
            int dim = entity.Pos.Dimension;
            float yaw = entity.Pos.Yaw;

            int count = Math.Max(1, stage.cloneCount);
            for (int i = 0; i < count; i++)
            {
                Entity clone = Sapi.World.ClassRegistry.CreateEntity(type);
                if (clone == null) continue;

                CopyTargetId(clone);
                CopyAnchor(clone);
                ApplyCloneAttributes(clone, stage);

                Vec3d offset = RandomOffset(stage.spawnRange);
                clone.Pos.SetPosWithDimension(new Vec3d(basePos.X + offset.X, basePos.Y + dim * 32768.0, basePos.Z + offset.Z));
                clone.Pos.Yaw = yaw + (float)((Sapi.World.Rand.NextDouble() - 0.5) * 0.4);
                clone.Pos.SetFrom(clone.Pos);

                Sapi.World.SpawnEntity(clone);

                activeCloneEntityIds.Add(clone.EntityId);
            }
        }

        private Vec3d RandomOffset(float range)
        {
            double r = range;
            if (r < 0.5) r = 0.5;

            double angle = Sapi.World.Rand.NextDouble() * Math.PI * 2.0;
            double dist = Sapi.World.Rand.NextDouble() * r;
            return new Vec3d(Math.Cos(angle) * dist, 0, Math.Sin(angle) * dist);
        }

        private void ApplyCloneAttributes(Entity clone, Stage stage)
        {
            if (clone?.WatchedAttributes == null) return;

            clone.WatchedAttributes.SetBool("showHealthbar", false);
            clone.WatchedAttributes.MarkPathDirty("showHealthbar");

            clone.WatchedAttributes.SetBool("alegacyvsquest:bossclone", true);
            clone.WatchedAttributes.MarkPathDirty("alegacyvsquest:bossclone");

            clone.WatchedAttributes.SetLong(CloneOwnerIdKey, entity.EntityId);
            clone.WatchedAttributes.MarkPathDirty(CloneOwnerIdKey);

            clone.WatchedAttributes.SetBool("alegacyvsquest:bossclone:invulnerable", stage.cloneInvulnerable);
            clone.WatchedAttributes.MarkPathDirty("alegacyvsquest:bossclone:invulnerable");

            if (stage.cloneDamageMult > 0f)
            {
                clone.WatchedAttributes.SetFloat("alegacyvsquest:bossclone:damagemult", stage.cloneDamageMult);
                clone.WatchedAttributes.MarkPathDirty("alegacyvsquest:bossclone:damagemult");
            }

            if (stage.cloneWalkSpeedMult > 0f)
            {
                clone.WatchedAttributes.SetFloat("alegacyvsquest:bossclone:walkspeedmult", stage.cloneWalkSpeedMult);
                clone.WatchedAttributes.MarkPathDirty("alegacyvsquest:bossclone:walkspeedmult");
            }

            clone.WatchedAttributes.SetBool("alegacyvsquest:bossclone:followowner", stage.cloneFollowOwner);
            clone.WatchedAttributes.MarkPathDirty("alegacyvsquest:bossclone:followowner");
        }

        private void CleanupClones()
        {
            if (Sapi == null) return;

            for (int i = 0; i < activeCloneEntityIds.Count; i++)
            {
                long id = activeCloneEntityIds[i];
                if (id <= 0) continue;

                var e = Sapi.World.GetEntityById(id);
                if (e != null)
                {
                    Sapi.World.DespawnEntity(e, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                }
            }

            activeCloneEntityIds.Clear();
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            CleanupClones();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            CleanupClones();
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

        private bool IsCloneEntity()
        {
            return entity?.WatchedAttributes?.GetBool("alegacyvsquest:bossclone", false) ?? false;
        }

        private void DespawnIfOwnerMissing()
        {
            if (Sapi == null || entity == null) return;

            long ownerId = entity.WatchedAttributes.GetLong(CloneOwnerIdKey, 0);

            if (ownerId <= 0)
            {
                Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                return;
            }

            var owner = Sapi.World.GetEntityById(ownerId);
            if (owner == null || !owner.Alive)
            {
                Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                return;
            }

            // Check if this clone should follow owner
            bool followOwner = entity.WatchedAttributes.GetBool("alegacyvsquest:bossclone:followowner", false);

            if (!followOwner) return;

            // Follow the owner - teleport if too far away
            double dx = entity.Pos.X - owner.Pos.X;
            double dz = entity.Pos.Z - owner.Pos.Z;
            double distSq = dx * dx + dz * dz;
            double maxDist = 25.0; // Max distance before teleport

            if (distSq > maxDist * maxDist)
            {
                // Teleport clone near the owner
                double angle = Sapi.World.Rand.NextDouble() * Math.PI * 2.0;
                double offsetDist = 3.0 + Sapi.World.Rand.NextDouble() * 4.0;
                double newX = owner.Pos.X + Math.Cos(angle) * offsetDist;
                double newZ = owner.Pos.Z + Math.Sin(angle) * offsetDist;

                entity.Pos.SetPos(newX, owner.Pos.Y, newZ);
                entity.Pos.SetFrom(entity.Pos);
            }
        }

        // Required abstract overrides for BossAbilityBase (event-driven mode)
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => index >= 0 && index < stages.Count ? stages[index] : null;
        protected override float GetStageHealthThreshold(object stage) => stage is Stage s ? s.whenHealthRelBelow : 1f;
        protected override float GetStageCooldown(object stage) => stage is Stage s ? s.cooldownSeconds : 0f;
        protected override float GetMaxTargetRange(object stage) => 0f;
    }
}
