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
    public class EntityBehaviorBossIntermissionDispel : BossAbilityBase
    {
        private const string StageKey = "alegacyvsquest:bossintermissiondispel:stage";
        protected override string CooldownKey => "alegacyvsquest:bossintermissiondispel:lastStartMs";
        protected override bool UseHealthBasedStages() => false;
        protected override bool RequiresTarget() => false;
        protected override int CheckIntervalMs => 500;

        private const string DispelFlagKey = "alegacyvsquest:bossintermissiondispel:dispel";
        private const string DispelOwnerIdKey = "alegacyvsquest:bossintermissiondispel:ownerid";

        private class Spawn
        {
            public string entityCode;
            public int maxNearby;
            public float nearbyRange;
            public int minCount;
            public int maxCount;
            public float chance;
            public int spawnDelayMs;
        }

        private class Stage : BossAbilityStage
        {
            public int intermissionMaxMs;
            public bool freezeBoss;
            public bool lockYaw;
            public float incomingDamageMultiplier;

            public float spawnRange;

            public List<Spawn> adds;

            public string dispelEntityCode;
            public int dispelCount;
            public bool dispelInvulnerable;

            public string animation;
            public int animationStopMs;

            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;

            public string loopSound;
            public float loopSoundRange;
            public int loopSoundIntervalMs;
            public float loopSoundVolume;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                intermissionMaxMs = json["intermissionMaxMs"].AsInt(20000);
                freezeBoss = json["freezeBoss"].AsBool(true);
                lockYaw = json["lockYaw"].AsBool(true);
                incomingDamageMultiplier = json["incomingDamageMultiplier"].AsFloat(0f);
                spawnRange = json["spawnRange"].AsFloat(8f);
                dispelEntityCode = json["dispelEntityCode"].AsString(null);
                dispelCount = json["dispelCount"].AsInt(1);
                dispelInvulnerable = json["dispelInvulnerable"].AsBool(false);
                animation = json["animation"].AsString(null);
                animationStopMs = json["animationStopMs"].AsInt(0);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(24f);
                soundStartMs = json["soundStartMs"].AsInt(0);
                soundVolume = json["soundVolume"].AsFloat(1f);
                loopSound = json["loopSound"].AsString(null);
                loopSoundRange = json["loopSoundRange"].AsFloat(24f);
                loopSoundIntervalMs = json["loopSoundIntervalMs"].AsInt(900);
                loopSoundVolume = json["loopSoundVolume"].AsFloat(1f);

                adds = new List<Spawn>();
                foreach (var addObj in json["adds"].AsArray())
                {
                    if (addObj == null || !addObj.Exists) continue;
                    var add = new Spawn
                    {
                        entityCode = addObj["entityCode"].AsString(null),
                        maxNearby = addObj["maxNearby"].AsInt(0),
                        nearbyRange = addObj["nearbyRange"].AsFloat(0f),
                        minCount = addObj["minCount"].AsInt(1),
                        maxCount = addObj["maxCount"].AsInt(1),
                        chance = addObj["chance"].AsFloat(1f),
                        spawnDelayMs = addObj["spawnDelayMs"].AsInt(0),
                    };
                    if (!string.IsNullOrWhiteSpace(add.entityCode)) adds.Add(add);
                }
            }
        }

        private List<Stage> stages = new List<Stage>();

        private long endsAtMs;
        private long startedAtMs;
        private int activeStageIndex = -1;

        private bool yawLocked;
        private float lockedYaw;

        private readonly BossBehaviorUtils.LoopSound loopSoundPlayer = new BossBehaviorUtils.LoopSound();

        private readonly List<long> spawnedAddIds = new List<long>();
        private readonly List<long> spawnedDispelIds = new List<long>();

        public EntityBehaviorBossIntermissionDispel(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossintermissiondispel";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        public override void OnGameTick(float dt)
        {
            if (IsDispelEntity())
            {
                DespawnIfOwnerMissing();
                return;
            }
            base.OnGameTick(dt);
        }

        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => 0f;

        protected override bool ShouldCheckAbility()
        {
            return !IsAbilityActive && !IsDispelEntity();
        }

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;

            MarkCooldownStart();
            SetAbilityActive(true);
            entity.WatchedAttributes.SetInt(StageKey, stageIndex + 1);
            entity.WatchedAttributes.MarkPathDirty(StageKey);

            StartIntermission(stage, stageIndex);
        }

        protected override void StopAbility()
        {
            StopIntermission();
        }

        protected override bool OnAbilityTick(float dt)
        {
            if (!IsAbilityActive) return false;
            if (activeStageIndex < 0 || activeStageIndex >= stages.Count) return false;

            var stage = stages[activeStageIndex];
            if (stage.freezeBoss) entity.StopAiAndFreeze();
            if (stage.lockYaw) entity.ApplyRotationLock(ref yawLocked, ref lockedYaw);

            if (AllObjectivesCleared() || Sapi.World.ElapsedMilliseconds >= endsAtMs)
            {
                StopIntermission();
                return false;
            }
            return true;
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (!IsAbilityActive) return;
            if (activeStageIndex < 0 || activeStageIndex >= stages.Count) return;

            float mult = stages[activeStageIndex].incomingDamageMultiplier;
            if (mult >= 0f && mult < 0.9999f)
            {
                damage *= mult;
            }
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            StopIntermission();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopIntermission();
            base.OnEntityDespawn(despawn);
        }

        private void StartIntermission(Stage stage, int stageIndex)
        {
            if (Sapi == null || entity == null || stage == null) return;

            SetAbilityActive(true);
            activeStageIndex = stageIndex;
            yawLocked = false;

            startedAtMs = Sapi.World.ElapsedMilliseconds;
            endsAtMs = startedAtMs + Math.Max(500, stage.intermissionMaxMs);

            spawnedAddIds.Clear();
            spawnedDispelIds.Clear();

            if (stage.freezeBoss)
            {
                entity.StopAiAndFreeze();
            }

            if (stage.lockYaw)
            {
                entity.ApplyRotationLock(ref yawLocked, ref lockedYaw);
            }

            TryPlaySound(stage);
            TryStartLoopSound(stage);
            TryPlayAnimation(stage);

            SpawnAdds(stage);
            SpawnDispelObjects(stage);
        }

        private void StopIntermission()
        {
            if (!IsAbilityActive && activeStageIndex < 0) return;

            SetAbilityActive(false);
            yawLocked = false;

            startedAtMs = 0;
            endsAtMs = 0;

            loopSoundPlayer.Stop();

            DespawnSpawnedAdds();
            DespawnSpawnedDispels();

            if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
            {
                var stage = stages[activeStageIndex];
                entity?.AnimManager?.StopAnimation(stage.animation);
            }

            activeStageIndex = -1;
        }

        private void DespawnSpawnedAdds()
        {
            if (Sapi == null) return;

            for (int i = spawnedAddIds.Count - 1; i >= 0; i--)
            {
                var e = Sapi.World.GetEntityById(spawnedAddIds[i]);
                if (e == null) continue;
                Sapi.World.DespawnEntity(e, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            }

            spawnedAddIds.Clear();
        }

        private void DespawnSpawnedDispels()
        {
            if (Sapi == null) return;

            for (int i = spawnedDispelIds.Count - 1; i >= 0; i--)
            {
                var e = Sapi.World.GetEntityById(spawnedDispelIds[i]);
                if (e == null) continue;
                Sapi.World.DespawnEntity(e, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            }

            spawnedDispelIds.Clear();
        }

        private bool AllObjectivesCleared()
        {
            for (int i = spawnedDispelIds.Count - 1; i >= 0; i--)
            {
                var e = Sapi.World.GetEntityById(spawnedDispelIds[i]);
                if (e == null || !e.Alive)
                {
                    spawnedDispelIds.RemoveAt(i);
                }
            }

            for (int i = spawnedAddIds.Count - 1; i >= 0; i--)
            {
                var e = Sapi.World.GetEntityById(spawnedAddIds[i]);
                if (e == null || !e.Alive)
                {
                    spawnedAddIds.RemoveAt(i);
                }
            }

            if (spawnedDispelIds.Count > 0) return false;
            if (spawnedAddIds.Count > 0) return false;

            return true;
        }

        private void SpawnAdds(Stage stage)
        {
            if (Sapi == null || entity == null || stage == null) return;
            if (stage.adds == null || stage.adds.Count == 0) return;

            for (int i = 0; i < stage.adds.Count; i++)
            {
                SpawnAdds(stage, stage.adds[i]);
            }
        }

        private void SpawnAdds(Stage stage, Spawn spawn)
        {
            if (Sapi == null || entity == null || stage == null || spawn == null) return;
            if (string.IsNullOrWhiteSpace(spawn.entityCode)) return;

            float chance = spawn.chance;
            if (chance <= 0f) return;
            if (chance < 1f && Sapi.World.Rand.NextDouble() > chance) return;

            int min = Math.Max(1, spawn.minCount);
            int max = Math.Max(min, spawn.maxCount);
            int count = min;
            if (max > min)
            {
                count = min + Sapi.World.Rand.Next(max - min + 1);
            }

            if (spawn.maxNearby > 0)
            {
                float range = spawn.nearbyRange > 0f ? spawn.nearbyRange : Math.Max(1f, stage.spawnRange);
                int aliveNearby = CountAliveNearby(spawn.entityCode, range);
                int remaining = spawn.maxNearby - aliveNearby;
                if (remaining <= 0) return;
                if (count > remaining) count = remaining;
            }

            var type = Sapi.World.GetEntityType(new AssetLocation(spawn.entityCode));
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

                if (spawn.spawnDelayMs > 0)
                {
                    Sapi.Event.RegisterCallback(_ =>
                    {
                        SpawnAddEntityAt(type, spawnPos, yaw);
                    }, spawn.spawnDelayMs);
                }
                else
                {
                    SpawnAddEntityAt(type, spawnPos, yaw);
                }
            }
        }

        private void SpawnAddEntityAt(EntityProperties type, Vec3d spawnPos, float yaw)
        {
            if (Sapi == null || type == null) return;

            Entity spawned = Sapi.World.ClassRegistry.CreateEntity(type);
            if (spawned == null) return;

            spawned.Pos.SetPosWithDimension(spawnPos);
            spawned.Pos.SetFrom(spawned.Pos);
            spawned.Pos.Yaw = yaw;

            Sapi.World.SpawnEntity(spawned);

            spawnedAddIds.Add(spawned.EntityId);
        }

        private void SpawnDispelObjects(Stage stage)
        {
            if (Sapi == null || entity == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.dispelEntityCode)) return;

            int count = Math.Max(1, stage.dispelCount);
            var type = Sapi.World.GetEntityType(new AssetLocation(stage.dispelEntityCode));
            if (type == null) return;

            int dim = entity.Pos.Dimension;
            for (int i = 0; i < count; i++)
            {
                Entity dispel = Sapi.World.ClassRegistry.CreateEntity(type);
                if (dispel == null) continue;

                ApplyDispelFlags(dispel, stage);

                double angle = Sapi.World.Rand.NextDouble() * Math.PI * 2.0;
                double dist = stage.spawnRange * (0.5 + Sapi.World.Rand.NextDouble() * 0.5);
                double x = entity.Pos.X + Math.Cos(angle) * dist;
                double z = entity.Pos.Z + Math.Sin(angle) * dist;
                double y = entity.Pos.Y;

                dispel.Pos.SetPosWithDimension(new Vec3d(x, y + dim * 32768.0, z));
                dispel.Pos.SetFrom(dispel.Pos);
                dispel.Pos.Yaw = (float)(Sapi.World.Rand.NextDouble() * Math.PI * 2.0);

                Sapi.World.SpawnEntity(dispel);

                spawnedDispelIds.Add(dispel.EntityId);
            }
        }

        private void ApplyDispelFlags(Entity dispel, Stage stage)
        {
            if (dispel?.WatchedAttributes == null || stage == null) return;

            dispel.WatchedAttributes.SetBool(DispelFlagKey, true);
            dispel.WatchedAttributes.MarkPathDirty(DispelFlagKey);

            dispel.WatchedAttributes.SetLong(DispelOwnerIdKey, entity.EntityId);
            dispel.WatchedAttributes.MarkPathDirty(DispelOwnerIdKey);

            dispel.WatchedAttributes.SetBool("alegacyvsquest:bossclone:invulnerable", stage.dispelInvulnerable);
            dispel.WatchedAttributes.MarkPathDirty("alegacyvsquest:bossclone:invulnerable");

            dispel.WatchedAttributes.SetBool("showHealthbar", false);
            dispel.WatchedAttributes.MarkPathDirty("showHealthbar");
        }

        private bool IsDispelEntity()
        {
            return entity?.WatchedAttributes?.GetBool(DispelFlagKey, false) ?? false;
        }

        private void DespawnIfOwnerMissing()
        {
            if (Sapi == null || entity == null) return;

            long ownerId = entity.WatchedAttributes.GetLong(DispelOwnerIdKey, 0);

            if (ownerId <= 0)
            {
                Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                return;
            }

            var owner = Sapi.World.GetEntityById(ownerId);
            if (owner == null || !owner.Alive)
            {
                Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            }
        }

        private int CountAliveNearby(string entityCode, float range)
        {
            if (Sapi == null || entity == null) return 0;
            if (string.IsNullOrWhiteSpace(entityCode)) return 0;
            if (range <= 0f) return 0;

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

        private void TryPlayAnimation(Stage stage)
        {
            if (stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.animation)) return;

            entity?.AnimManager?.StartAnimation(stage.animation);

            int stopMs = stage.animationStopMs;
            if (stopMs <= 0) return;

            Sapi.Event.RegisterCallback(_ =>
            {
                entity?.AnimManager?.StopAnimation(stage.animation);
            }, stopMs);
        }

        private void TryPlaySound(Stage stage)
        {
            if (Sapi == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.sound)) return;

            AssetLocation soundLoc = AssetLocation.Create(stage.sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            float range = stage.soundRange > 0f ? stage.soundRange : 32f;

            if (stage.soundStartMs > 0)
            {
                Sapi.Event.RegisterCallback(_ =>
                {
                    float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                    Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, range, stage.soundVolume);
                }, stage.soundStartMs);
            }
            else
            {
                float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, range, stage.soundVolume);
            }
        }

        private void TryStartLoopSound(Stage stage)
        {
            if (Sapi == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.loopSound)) return;

            loopSoundPlayer.Start(Sapi, entity, stage.loopSound, stage.loopSoundRange, stage.loopSoundIntervalMs, stage.loopSoundVolume);
        }
    }
}
