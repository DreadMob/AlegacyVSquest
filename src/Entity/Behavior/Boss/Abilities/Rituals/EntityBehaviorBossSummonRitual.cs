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
    public class EntityBehaviorBossSummonRitual : BossAbilityBase
    {
        private const string SummonStageKey = "alegacyvsquest:bosssummonstage";
        private const string SummonedByEntityIdKey = "alegacyvsquest:bosssummonritual:summonedByEntityId";
        private const string SummonedByEntityCodeKey = "alegacyvsquest:bosssummonritual:summonedByEntityCode";

        protected override string CooldownKey => "alegacyvsquest:bosssummonritual:lastStartMs";
        protected override bool UseHealthBasedStages() => false;
        protected override bool RequiresTarget() => false;
        protected override int CheckIntervalMs => 500;

        private class SummonSpawn
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
            public string entityCode;
            public int maxNearby;
            public float nearbyRange;
            public int minCount;
            public int maxCount;
            public List<SummonSpawn> spawns;
            public int ritualMs;
            public float healPerSecond;
            public float healRelPerSecond;
            public float spawnRange;
            public float circleRadius;
            public float circleMoveSpeed;
            public int circleStartDelayMs;
            public string animation;
            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;
            public string loopSound;
            public float loopSoundRange;
            public int loopSoundIntervalMs;
            public float loopSoundVolume;
            public int spawnDelayMs;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                entityCode = json["entityCode"].AsString(null);
                maxNearby = json["maxNearby"].AsInt(0);
                nearbyRange = json["nearbyRange"].AsFloat(0f);
                minCount = json["minCount"].AsInt(1);
                maxCount = json["maxCount"].AsInt(1);
                ritualMs = json["ritualMs"].AsInt(4000);
                healPerSecond = json["healPerSecond"].AsFloat(0f);
                healRelPerSecond = json["healRelPerSecond"].AsFloat(0f);
                spawnRange = json["spawnRange"].AsFloat(6f);
                circleRadius = json["circleRadius"].AsFloat(0f);
                circleMoveSpeed = json["circleMoveSpeed"].AsFloat(0f);
                circleStartDelayMs = json["circleStartDelayMs"].AsInt(0);
                animation = json["animation"].AsString(null);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(16f);
                soundStartMs = json["soundStartMs"].AsInt(0);
                soundVolume = json["soundVolume"].AsFloat(1f);
                loopSound = json["loopSound"].AsString(null);
                loopSoundRange = json["loopSoundRange"].AsFloat(16f);
                loopSoundIntervalMs = json["loopSoundIntervalMs"].AsInt(900);
                loopSoundVolume = json["loopSoundVolume"].AsFloat(1f);
                spawnDelayMs = json["spawnDelayMs"].AsInt(600);

                spawns = new List<SummonSpawn>();
                if (json["spawns"]?.Exists == true)
                {
                    foreach (var spawnObj in json["spawns"].AsArray())
                    {
                        if (spawnObj == null || !spawnObj.Exists) continue;

                        var spawn = new SummonSpawn
                        {
                            entityCode = spawnObj["entityCode"].AsString(null),
                            maxNearby = spawnObj["maxNearby"].AsInt(maxNearby),
                            nearbyRange = spawnObj["nearbyRange"].AsFloat(nearbyRange),
                            minCount = spawnObj["minCount"].AsInt(minCount),
                            maxCount = spawnObj["maxCount"].AsInt(maxCount),
                            chance = spawnObj["chance"].AsFloat(1f),
                            spawnDelayMs = spawnObj["spawnDelayMs"].AsInt(spawnDelayMs),
                        };

                        if (!string.IsNullOrWhiteSpace(spawn.entityCode))
                        {
                            spawns.Add(spawn);
                        }
                    }
                }
            }
        }

        private List<Stage> stages = new List<Stage>();
        private long ritualEndsAtMs;
        private long ritualStartedAtMs;
        private readonly BossBehaviorUtils.LoopSound soundLoop = new BossBehaviorUtils.LoopSound();
        private int activeStageIndex = -1;
        private float lockedYaw;
        private bool yawLocked;

        private Vec3d ritualCenter;
        private float ritualCircleAngle;

        public EntityBehaviorBossSummonRitual(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosssummonritual";

        public bool IsRitualActive => IsAbilityActive;

        protected override void InitializeStages(JsonObject attributes)
        {
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
            entity.WatchedAttributes.SetInt(SummonStageKey, stageIndex + 1);
            entity.WatchedAttributes.MarkPathDirty(SummonStageKey);
            StartRitual(stage, stageIndex);
        }

        protected override void StopAbility()
        {
            StopRitual();
        }

        protected override bool OnAbilityTick(float dt)
        {
            if (!IsAbilityActive) return false;

            entity.ApplyRotationLock(ref yawLocked, ref lockedYaw);
            if (stages.Count > activeStageIndex && activeStageIndex >= 0)
            {
                var activeStage = stages[activeStageIndex];
                HealDuringRitual(activeStage, dt);
                ApplyCircleMovement(activeStage, dt);
            }

            if (Sapi.World.ElapsedMilliseconds >= ritualEndsAtMs)
            {
                StopRitual();
                return false;
            }
            return true;
        }

        private void TryStartLoopSound(Stage stage)
        {
            if (string.IsNullOrWhiteSpace(stage.loopSound)) return;
            float volume = stage.loopSoundVolume;
            if (volume <= 0f) volume = 1f;
            soundLoop.Start(Sapi, entity, stage.loopSound, stage.loopSoundRange, stage.loopSoundIntervalMs, volume);
        }

        private void StartRitual(Stage stage, int index)
        {
            SetAbilityActive(true);
            activeStageIndex = index;
            ritualStartedAtMs = Sapi.World.ElapsedMilliseconds;
            ritualEndsAtMs = ritualStartedAtMs + Math.Max(500, stage.ritualMs);

            ritualCircleAngle = (float)(Sapi.World.Rand.NextDouble() * Math.PI * 2.0);
            ritualCenter = new Vec3d(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
            if (stage.circleRadius > 0f)
            {
                double radius = Math.Max(0.25, stage.circleRadius);
                ritualCenter.X = entity.Pos.X - Math.Cos(ritualCircleAngle) * radius;
                ritualCenter.Z = entity.Pos.Z - Math.Sin(ritualCircleAngle) * radius;
            }

            entity.StopAiAndFreeze();
            entity.ApplyRotationLock(ref yawLocked, ref lockedYaw);
            SpawnMinions(stage);
            TryPlaySound(stage);
            TryStartLoopSound(stage);
            TryPlayAnimation(stage.animation);
        }

        private void StopRitual()
        {
            SetAbilityActive(false);
            yawLocked = false;

            ritualStartedAtMs = 0;

            ritualCenter = null;

            soundLoop.Stop();

            if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
            {
                var animation = stages[activeStageIndex].animation;
                if (!string.IsNullOrWhiteSpace(animation))
                {
                    entity?.AnimManager?.StopAnimation(animation);
                }
            }

            activeStageIndex = -1;
        }

        private void ApplyCircleMovement(Stage stage, float dt)
        {
            if (stage == null || ritualCenter == null || stage.circleRadius <= 0f || stage.circleMoveSpeed <= 0f) return;

            if (stage.circleStartDelayMs > 0 && Sapi != null && ritualStartedAtMs > 0)
            {
                if (Sapi.World.ElapsedMilliseconds - ritualStartedAtMs < stage.circleStartDelayMs) return;
            }

            float radius = Math.Max(0.25f, stage.circleRadius);
            float moveSpeed = Math.Max(0.001f, stage.circleMoveSpeed);
            float angSpeed = moveSpeed / radius;

            ritualCircleAngle += angSpeed * dt;

            double x = ritualCenter.X + Math.Cos(ritualCircleAngle) * radius;
            double z = ritualCenter.Z + Math.Sin(ritualCircleAngle) * radius;

            int dim = entity.Pos.Dimension;
            double y = entity.Pos.Y;
            entity.Pos.SetPosWithDimension(new Vec3d(x, y + dim * 32768.0, z));
            entity.Pos.SetFrom(entity.Pos);
        }

        private void HealDuringRitual(Stage stage, float dt)
        {
            if (stage.healPerSecond <= 0f && stage.healRelPerSecond <= 0f) return;

            if (!entity.TryGetHealth(out var healthTree, out float curHealth, out float maxHealth)) return;

            float absHeal = stage.healPerSecond > 0f ? stage.healPerSecond * dt : 0f;
            float relHeal = stage.healRelPerSecond > 0f ? stage.healRelPerSecond * maxHealth * dt : 0f;
            float heal = absHeal + relHeal;
            if (heal <= 0f) return;

            float newHealth = Math.Min(maxHealth, curHealth + heal);
            healthTree.SetFloat("currenthealth", newHealth);
            entity.WatchedAttributes.MarkPathDirty("health");
        }

        private void SpawnMinions(Stage stage)
        {
            if (Sapi == null) return;
            if (stage == null) return;

            if (stage.spawns != null && stage.spawns.Count > 0)
            {
                for (int i = 0; i < stage.spawns.Count; i++)
                {
                    SpawnMinions(stage, stage.spawns[i]);
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(stage.entityCode)) return;
            SpawnMinions(stage, new SummonSpawn
            {
                entityCode = stage.entityCode,
                maxNearby = stage.maxNearby,
                nearbyRange = stage.nearbyRange,
                minCount = stage.minCount,
                maxCount = stage.maxCount,
                chance = 1f,
                spawnDelayMs = stage.spawnDelayMs
            });
        }

        private void SpawnMinions(Stage stage, SummonSpawn spawn)
        {
            if (Sapi == null || entity == null) return;
            if (stage == null || spawn == null) return;
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

            EntityProperties type = null;
            AssetLocation codeLoc = new AssetLocation(spawn.entityCode);
            type = Sapi.World.GetEntityType(codeLoc);

            // Fallback: some vanilla entities live in the 'survival' asset domain.
            if (type == null && string.Equals(codeLoc.Domain, "game", StringComparison.OrdinalIgnoreCase))
            {
                var survivalLoc = new AssetLocation("survival", codeLoc.Path);
                type = Sapi.World.GetEntityType(survivalLoc);
            }

            if (type == null)
            {
                Sapi.Logger.Warning("[VsQuest] bosssummonritual: entity type not found for code '{0}'", spawn.entityCode);
                return;
            }

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
                        SpawnEntityAt(type, spawnPos, yaw);
                    }, spawn.spawnDelayMs);
                }
                else
                {
                    SpawnEntityAt(type, spawnPos, yaw);
                }
            }
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

        private void SpawnEntityAt(EntityProperties type, Vec3d spawnPos, float yaw)
        {
            if (Sapi == null || type == null) return;

            Entity spawned = Sapi.World.ClassRegistry.CreateEntity(type);
            if (spawned == null) return;

            if (entity != null && entity.EntityId != 0)
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

        private void TryDisableFleeForSummonedWolves(Entity spawned)
        {
            if (Sapi == null || spawned == null) return;

            var code = spawned.Code?.ToString() ?? "";
            if (code.IndexOf("wolf", StringComparison.OrdinalIgnoreCase) < 0) return;

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

        private void DespawnSummonedMinions()
        {
            if (Sapi == null || entity == null) return;

            int dim = entity.Pos.Dimension;
            var center = new Vec3d(entity.Pos.X, entity.Pos.Y + dim * 32768.0, entity.Pos.Z);

            const float range = 64f;
            var entities = Sapi.World.GetEntitiesAround(center, range, range, e => e != null && e.Alive);
            if (entities == null) return;

            long ownerId = entity.EntityId;
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null) continue;
                if (e.EntityId == ownerId) continue;

                long summonedBy = e.WatchedAttributes.GetLong(SummonedByEntityIdKey, 0);
                if (summonedBy != ownerId) continue;

                Sapi.World.DespawnEntity(e, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            }
        }

        private void TryPlaySound(Stage stage)
        {
            if (string.IsNullOrWhiteSpace(stage.sound)) return;

            AssetLocation soundLoc = AssetLocation.Create(stage.sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            float pitchMul = entity?.Properties?.Attributes?["vsquestSoundPitchMul"].AsFloat(1f) ?? 1f;
            if (pitchMul <= 0f) pitchMul = 1f;

            float range = stage.soundRange > 0f ? stage.soundRange : 32f;

            if (stage.soundStartMs > 0)
            {
                Sapi.Event.RegisterCallback(_ =>
                {
                    float volume = stage.soundVolume;
                    if (volume <= 0f) volume = 1f;

                    float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                    pitch *= pitchMul;
                    Sapi.World.PlaySoundAt(soundLoc, entity.Pos.X, entity.Pos.Y, entity.Pos.Z, null, pitch, range, volume);
                }, stage.soundStartMs);
            }
            else
            {
                float volume = stage.soundVolume;
                if (volume <= 0f) volume = 1f;

                float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                pitch *= pitchMul;
                Sapi.World.PlaySoundAt(soundLoc, entity.Pos.X, entity.Pos.Y, entity.Pos.Z, null, pitch, range, volume);
            }
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            StopRitual();
            DespawnSummonedMinions();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopRitual();
            base.OnEntityDespawn(despawn);
        }

        // Required abstract overrides for BossAbilityBase (event-driven mode)
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => index >= 0 && index < stages.Count ? stages[index] : null;
        protected override float GetStageHealthThreshold(object stage) => stage is Stage s ? s.whenHealthRelBelow : 1f;
        protected override float GetStageCooldown(object stage) => stage is Stage s ? s.cooldownSeconds : 0f;
        protected override float GetMaxTargetRange(object stage) => 0f;
    }
}
