using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Boss cloning ability: spawns permanent clones of the boss at health thresholds.
    /// Clones persist indefinitely, attack players, and more spawn at lower health.
    /// </summary>
    public class EntityBehaviorBossCloning : BossAbilityBase
    {
        private const string CloneStageKey = "alegacyvsquest:bossclonestage";
        private const string CloneOwnerIdKey = "alegacyvsquest:bossclone:ownerid";

        private const string TargetIdKey = "alegacyvsquest:killaction:targetid";
        private const string AnchorKeyPrefix = "alegacyvsquest:spawner:";

        protected override string CooldownKey => "alegacyvsquest:bossclone:lastStartMs";
        protected override bool UseHealthBasedStages() => true;
        protected override bool RequiresTarget() => false;
        protected override int CheckIntervalMs => 500;

        private class Stage : BossAbilityStage
        {
            public int cloneCount;
            public float spawnRange;
            public float cloneDamageMult;
            public float cloneWalkSpeedMult;
            public bool cloneInvulnerable;
            public bool cloneFollowOwner;

            // Optional visual feedback
            public string animation;
            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                cloneCount = json["cloneCount"].AsInt(2);
                spawnRange = json["spawnRange"].AsFloat(6f);
                cloneDamageMult = json["cloneDamageMult"].AsFloat(0.35f);
                cloneWalkSpeedMult = json["cloneWalkSpeedMult"].AsFloat(0.85f);
                cloneInvulnerable = json["cloneInvulnerable"].AsBool(true);
                cloneFollowOwner = json["cloneFollowOwner"].AsBool(false);

                // Optional visual feedback
                animation = json["animation"].AsString(null);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(24f);
                soundStartMs = json["soundStartMs"].AsInt(0);
                soundVolume = json["soundVolume"].AsFloat(0.6f);
            }
        }

        private List<Stage> stages = new List<Stage>();
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
            if (!base.ShouldCheckAbility()) return false;
            if (IsAbilityActive) return false;

            // Don't re-activate while clones still exist
            if (AnyCloneAlive()) return false;

            int currentStage = entity.WatchedAttributes.GetInt(CloneStageKey, 0);
            if (!entity.TryGetHealthFraction(out float frac)) return false;

            // Iterate HIGHEST threshold FIRST to find the best matching stage
            for (int i = stages.Count - 1; i >= 0; i--)
            {
                if (frac <= stages[i].whenHealthRelBelow)
                {
                    if (currentStage < i + 1)
                        return true;
                    return false;
                }
            }

            return false;
        }

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;

            MarkCooldownStart();

            entity.WatchedAttributes.SetInt(CloneStageKey, stageIndex + 1);
            entity.WatchedAttributes.MarkPathDirty(CloneStageKey);

            SetAbilityActive(true);

            // Play visual feedback before spawning
            TryPlayAnimation(stage.animation);
            TryPlaySound(stage.sound, stage.soundRange, stage.soundStartMs, stage.soundVolume);

            CleanupClones();
            SpawnClones(stage);
        }

        protected override void StopAbility()
        {
            StopCloning();
        }

        protected override bool OnAbilityTick(float dt)
        {
            if (!IsAbilityActive || Sapi == null || entity == null) return false;

            int currentStage = entity.WatchedAttributes.GetInt(CloneStageKey, 0);

            // Clean dead clone entries from tracking list
            for (int i = activeCloneEntityIds.Count - 1; i >= 0; i--)
            {
                long id = activeCloneEntityIds[i];
                if (id <= 0) { activeCloneEntityIds.RemoveAt(i); continue; }
                var e = Sapi.World.GetEntityById(id);
                if (e == null || !e.Alive)
                {
                    activeCloneEntityIds.RemoveAt(i);
                }
            }

            // Check for stage upgrade (more clones at lower health)
            if (currentStage < stages.Count && entity.TryGetHealthFraction(out float frac))
            {
                for (int i = stages.Count - 1; i >= 0; i--)
                {
                    if (frac <= stages[i].whenHealthRelBelow)
                    {
                        int newStage = i + 1;
                        if (newStage > currentStage)
                        {
                            // Upgrade stage - clean old clones, spawn new set
                            entity.WatchedAttributes.SetInt(CloneStageKey, newStage);
                            entity.WatchedAttributes.MarkPathDirty(CloneStageKey);
                            currentStage = newStage;

                            CleanupClones();

                            TryPlayAnimation(stages[i].animation);
                            TryPlaySound(stages[i].sound, stages[i].soundRange, stages[i].soundStartMs, stages[i].soundVolume);

                            SpawnClones(stages[i]);
                        }
                        break;
                    }
                }
            }

            // Replenish dead clones for current stage
            if (currentStage > 0 && currentStage <= stages.Count)
            {
                Stage stage = stages[currentStage - 1];
                int aliveCount = activeCloneEntityIds.Count;
                int targetCount = stage.cloneCount;

                if (aliveCount < targetCount)
                {
                    int toSpawn = Math.Min(targetCount - aliveCount, 10);
                    for (int i = 0; i < toSpawn; i++)
                    {
                        SpawnOneClone(stage);
                    }
                }
            }

            return true; // Keep active permanently
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            // Clone entity special handling
            if (IsCloneEntity())
            {
                DespawnIfOwnerMissing();
                return;
            }
        }

        private bool AnyCloneAlive()
        {
            if (Sapi == null) return false;
            for (int i = activeCloneEntityIds.Count - 1; i >= 0; i--)
            {
                long id = activeCloneEntityIds[i];
                if (id <= 0) continue;
                var e = Sapi.World.GetEntityById(id);
                if (e != null && e.Alive) return true;
            }
            return false;
        }

        private void StopCloning()
        {
            if (!IsAbilityActive) return;
            SetAbilityActive(false);
            CleanupClones();
        }

        private void SpawnClones(Stage stage)
        {
            if (Sapi == null || entity == null) return;

            int count = Math.Max(1, stage.cloneCount);
            for (int i = 0; i < count; i++)
            {
                SpawnOneClone(stage);
            }
        }

        private void SpawnOneClone(Stage stage)
        {
            if (Sapi == null || entity == null || stage == null) return;

            string code = entity.Code?.ToShortString();
            if (string.IsNullOrWhiteSpace(code)) return;

            var type = Sapi.World.GetEntityType(new AssetLocation(code));
            if (type == null) return;

            Vec3d basePos = entity.Pos.XYZ;
            int dim = entity.Pos.Dimension;

            Entity clone = Sapi.World.ClassRegistry.CreateEntity(type);
            if (clone == null) return;

            CopyTargetId(clone);
            CopyAnchor(clone);
            ApplyCloneAttributes(clone, stage);

            Vec3d offset = RandomOffset(stage.spawnRange);
            double px = basePos.X + offset.X;
            double pz = basePos.Z + offset.Z;

            // Validate spawn position is loaded
            if (!Sapi.World.BlockAccessor.IsValidPos(new BlockPos((int)px, (int)basePos.Y, (int)pz, dim)))
                return;

            clone.Pos.SetPosWithDimension(new Vec3d(px, basePos.Y, pz));
            clone.Pos.Yaw = entity.Pos.Yaw + (float)((Sapi.World.Rand.NextDouble() - 0.5) * 0.4);
            clone.Pos.SetFrom(clone.Pos);

            Sapi.World.SpawnEntity(clone);

            activeCloneEntityIds.Add(clone.EntityId);
        }

        private Vec3d RandomOffset(float range)
        {
            double r = Math.Max(0.5, range);
            double angle = Sapi.World.Rand.NextDouble() * Math.PI * 2.0;
            double dist = Sapi.World.Rand.NextDouble() * r;
            return new Vec3d(Math.Cos(angle) * dist, 0, Math.Sin(angle) * dist);
        }

        private void ApplyCloneAttributes(Entity clone, Stage stage)
        {
            if (clone?.WatchedAttributes == null) return;
            var wa = clone.WatchedAttributes;

            wa.SetBool("showHealthbar", false);
            wa.MarkPathDirty("showHealthbar");

            wa.SetBool("alegacyvsquest:bossclone", true);
            wa.MarkPathDirty("alegacyvsquest:bossclone");

            wa.SetLong(CloneOwnerIdKey, entity.EntityId);
            wa.MarkPathDirty(CloneOwnerIdKey);

            wa.SetBool("alegacyvsquest:bossclone:invulnerable", stage.cloneInvulnerable);
            wa.MarkPathDirty("alegacyvsquest:bossclone:invulnerable");

            if (stage.cloneDamageMult > 0f)
            {
                wa.SetFloat("alegacyvsquest:bossclone:damagemult", stage.cloneDamageMult);
                wa.MarkPathDirty("alegacyvsquest:bossclone:damagemult");
            }

            if (stage.cloneWalkSpeedMult > 0f)
            {
                wa.SetFloat("alegacyvsquest:bossclone:walkspeedmult", stage.cloneWalkSpeedMult);
                wa.MarkPathDirty("alegacyvsquest:bossclone:walkspeedmult");
            }

            wa.SetBool("alegacyvsquest:bossclone:followowner", stage.cloneFollowOwner);
            wa.MarkPathDirty("alegacyvsquest:bossclone:followowner");
        }

        private void CleanupClones()
        {
            if (Sapi == null) return;

            for (int i = activeCloneEntityIds.Count - 1; i >= 0; i--)
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
                Sapi.Logger.Notification("[BossCloning] Clone {0} has no owner, despawning", entity.EntityId);
                Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                return;
            }

            var owner = Sapi.World.GetEntityById(ownerId);
            if (owner == null)
            {
                if (entity.WatchedAttributes.GetBool("alegacyvsquest:clone:ownerchecked", false))
                {
                    Sapi.Logger.Notification("[BossCloning] Clone {0} owner {1} not found, despawning", entity.EntityId, ownerId);
                    Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                }
                return;
            }

            entity.WatchedAttributes.SetBool("alegacyvsquest:clone:ownerchecked", true);
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:clone:ownerchecked");

            if (!owner.Alive)
            {
                Sapi.Logger.Notification("[BossCloning] Clone {0} owner {1} not alive, despawning", entity.EntityId, ownerId);
                Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                return;
            }

            bool followOwner = entity.WatchedAttributes.GetBool("alegacyvsquest:bossclone:followowner", false);
            if (!followOwner) return;

            double dx = entity.Pos.X - owner.Pos.X;
            double dz = entity.Pos.Z - owner.Pos.Z;
            double distSq = dx * dx + dz * dz;

            if (distSq > 625.0) // 25^2
            {
                double angle = Sapi.World.Rand.NextDouble() * Math.PI * 2.0;
                double offsetDist = 3.0 + Sapi.World.Rand.NextDouble() * 4.0;
                double newX = owner.Pos.X + Math.Cos(angle) * offsetDist;
                double newZ = owner.Pos.Z + Math.Sin(angle) * offsetDist;

                entity.Pos.SetPos(newX, owner.Pos.Y, newZ);
                entity.Pos.SetFrom(entity.Pos);
            }
        }

        // Required abstract overrides for BossAbilityBase
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => index >= 0 && index < stages.Count ? stages[index] : null;
        protected override float GetStageHealthThreshold(object stage) => stage is Stage s ? s.whenHealthRelBelow : 1f;
        protected override float GetStageCooldown(object stage) => stage is Stage s ? s.cooldownSeconds : 0f;
        protected override float GetMaxTargetRange(object stage) => 0f;
    }
}
