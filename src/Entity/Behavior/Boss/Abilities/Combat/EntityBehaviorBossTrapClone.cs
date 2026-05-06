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
    public class EntityBehaviorBossTrapClone : BossAbilityBase
    {
        private static readonly HashSet<string> WarnedNoStagesEntityCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        protected override string CooldownKey => "alegacyvsquest:bosstrapclone:lastStartMs";
        protected override int CheckIntervalMs => 200;

        private const string TrapFlagKey = "alegacyvsquest:bosstrapclone:trap";
        private const string TrapOwnerIdKey = "alegacyvsquest:bosstrapclone:ownerid";
        private const string TrapExplodeAtMsKey = "alegacyvsquest:bosstrapclone:explodeat";
        private const string TrapRadiusKey = "alegacyvsquest:bosstrapclone:radius";
        private const string TrapDamageKey = "alegacyvsquest:bosstrapclone:damage";
        private const string TrapDamageTierKey = "alegacyvsquest:bosstrapclone:damagetier";
        private const string TrapDamageTypeKey = "alegacyvsquest:bosstrapclone:damagetype";
        private const string TrapExplodeSoundKey = "alegacyvsquest:bosstrapclone:explodesound";
        private const string TrapExplodeSoundRangeKey = "alegacyvsquest:bosstrapclone:explodesoundrange";
        private const string TrapExplodeSoundVolumeKey = "alegacyvsquest:bosstrapclone:explodesoundvolume";

        private class Stage : BossAbilityStage
        {
            public string trapEntityCode;
            public float spawnRange;
            public int trapCount;

            public int fuseMs;
            public float explosionRadius;
            public float explosionDamage;
            public int damageTier;
            public string damageType;

            public bool trapInvulnerable;

            public string windupAnimation;
            public int windupMs;

            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;

            public string explodeSound;
            public float explodeSoundRange;
            public float explodeSoundVolume;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                trapEntityCode = json["trapEntityCode"].AsString(null);
                spawnRange = json["spawnRange"].AsFloat(DefaultSpawnRange);
                trapCount = json["trapCount"].AsInt(DefaultTrapCount);
                fuseMs = json["fuseMs"].AsInt(DefaultFuseMs);
                explosionRadius = json["explosionRadius"].AsFloat(DefaultExplosionRadius);
                explosionDamage = json["explosionDamage"].AsFloat(DefaultExplosionDamage);
                damageTier = json["damageTier"].AsInt(DefaultDamageTier);
                damageType = json["damageType"].AsString(DefaultDamageType);
                trapInvulnerable = json["trapInvulnerable"].AsBool(false);
                windupAnimation = json["windupAnimation"].AsString(null);
                windupMs = json["windupMs"].AsInt(0);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(DefaultSoundRange);
                soundStartMs = json["soundStartMs"].AsInt(DefaultSoundStartMs);
                soundVolume = json["soundVolume"].AsFloat(DefaultSoundVolume);
                explodeSound = json["explodeSound"].AsString(null);
                explodeSoundRange = json["explodeSoundRange"].AsFloat(DefaultExplodeSoundRange);
                explodeSoundVolume = json["explodeSoundVolume"].AsFloat(DefaultExplodeSoundVolume);

                // Validation
                if (spawnRange <= 0f) spawnRange = MinSpawnRange;
                if (trapCount <= 0) trapCount = MinTrapCount;
                if (fuseMs <= 0) fuseMs = MinFuseMs;
                if (explosionRadius <= 0f) explosionRadius = MinExplosionRadius;
                if (explosionDamage < 0f) explosionDamage = 0f;
                if (damageTier < MinDamageTier) damageTier = MinDamageTier;
                if (windupMs < 0) windupMs = 0;
                if (soundVolume <= 0f) soundVolume = 1f;
                if (explodeSoundVolume <= 0f) explodeSoundVolume = 1f;
            }
        }

        private List<Stage> stages = new List<Stage>();
        private const int TrapOwnerCheckIntervalMs = 500;
        private const int ExplodeRetryDelayMs = 250;
        private const int ExplodeSoundLimiterMs = 400;
        private const int SpawnSoundLimiterMs = 500;
        private const float DefaultMaxTargetRange = 40f;
        private const float DefaultSpawnRange = 6f;
        private const int DefaultTrapCount = 1;
        private const int DefaultFuseMs = 1500;
        private const float DefaultExplosionRadius = 3f;
        private const float DefaultExplosionDamage = 6f;
        private const int DefaultDamageTier = 3;
        private const string DefaultDamageType = "PiercingAttack";
        private const float DefaultSoundRange = 24f;
        private const int DefaultSoundStartMs = 0;
        private const float DefaultSoundVolume = 1f;
        private const float DefaultExplodeSoundRange = 24f;
        private const float DefaultExplodeSoundVolume = 1f;

        private const float MinSpawnRange = 0.5f;
        private const int MinTrapCount = 1;
        private const int MinFuseMs = 250;
        private const float MinExplosionRadius = 0.5f;
        private const int MinDamageTier = 0;
        private const int SpawnTries = 14;
        private const bool SpawnRequireSolidGround = true;
        private const float SpawnMinRingFrac = 0.4f;
        private const float SpawnMinSeparationMin = 1.5f;
        private const float SpawnMinSeparationSpawnRangeFrac = 0.5f;
        private const float SpawnYawRandomRange = 0.4f;
        private const int FindFreeSpotMaxVerticalSteps = 6;

        private long lastTrapOwnerCheckMs;

        private long callbackId;

        public EntityBehaviorBossTrapClone(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosstrapclone";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
            if (stages.Count == 0 && entity?.WatchedAttributes?.GetBool(TrapFlagKey, false) != true)
            {
                string code = entity?.Code?.ToString();
                if (!string.IsNullOrWhiteSpace(code) && WarnedNoStagesEntityCodes.Add(code))
                {
                    entity.Api?.Logger?.Warning($"[BossTrapClone] No stages defined for {entity.Code}");
                }
            }
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (Sapi == null || entity == null) return;
            if (entity.Api?.Side != EnumAppSide.Server) return;

            if (IsTrapEntity())
            {
                TickTrap();
                return;
            }
        }

        protected override int GetStageCount() => stages.Count;

        protected override object GetStage(int index) => stages[index];

        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;

        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;

        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override float MinTargetRange => 0.75f;

        protected override bool ShouldCheckAbility()
        {
            if (IsTrapEntity()) return false;
            return !IsAbilityActive;
        }

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (target == null || stageObj is not Stage stage) return;
            Start(stage, target);
        }

        protected override void StopAbility()
        {
            CancelPending();
        }

        protected override bool OnAbilityTick(float dt) => IsAbilityActive;

        private void Start(Stage stage, EntityPlayer target)
        {
            if (Sapi == null || entity == null || stage == null || target == null) return;

            MarkCooldownStart();
            SetAbilityActive(true);

            BossBehaviorUtils.StopAiAndFreeze(entity);
            TryPlaySound(stage);
            TryPlayAnimation(stage.windupAnimation);

            int delay = Math.Max(0, stage.windupMs);
            UnregisterCallbackSafe(ref callbackId);
            callbackId = Sapi.Event.RegisterCallback(_ =>
            {
                SpawnTraps(stage, target);

                SetAbilityActive(false);
                callbackId = 0;

            }, delay);
        }

        private void SpawnTraps(Stage stage, EntityPlayer target)
        {
            if (Sapi == null || entity == null || stage == null || target == null) return;
            if (string.IsNullOrWhiteSpace(stage.trapEntityCode)) return;

            var type = Sapi.World.GetEntityType(new AssetLocation(stage.trapEntityCode));
            if (type == null) return;

            int dim = entity.Pos.Dimension;
            float yaw = entity.Pos.Yaw;

            int count = Math.Max(1, stage.trapCount);
            var placed = new List<Vec3d>();

            float minRingFrac = SpawnMinRingFrac;
            float minSeparation = Math.Max(SpawnMinSeparationMin, stage.spawnRange * SpawnMinSeparationSpawnRangeFrac);

            for (int i = 0; i < count; i++)
            {
                Entity trap = Sapi.World.ClassRegistry.CreateEntity(type);
                if (trap == null) continue;

                ApplyTrapFlags(trap, stage);

                var spawnPos = TryFindSpawnPositionNear(trap, target.Pos.XYZ, stage.spawnRange, tries: SpawnTries, requireSolidGround: SpawnRequireSolidGround, minRingFrac: minRingFrac, avoidPositions: placed, minSeparation: minSeparation);
                trap.Pos.SetPosWithDimension(new Vec3d(spawnPos.X, spawnPos.Y + dim * 32768.0, spawnPos.Z));
                double yawRand = (Sapi.World.Rand.NextDouble() - 0.5) * SpawnYawRandomRange;
                trap.Pos.Yaw = yaw + (float)yawRand;
                trap.Pos.SetFrom(trap.Pos);

                Sapi.World.SpawnEntity(trap);

                placed.Add(spawnPos);
            }
        }

        private Vec3d TryFindSpawnPositionNear(Entity trap, Vec3d center, float range, int tries, bool requireSolidGround)
        {
            return TryFindSpawnPositionNear(trap, center, range, tries, requireSolidGround, minRingFrac: 0f, avoidPositions: null, minSeparation: 0f);
        }

        private Vec3d TryFindSpawnPositionNear(Entity trap, Vec3d center, float range, int tries, bool requireSolidGround, float minRingFrac, List<Vec3d> avoidPositions, float minSeparation)
        {
            if (Sapi == null || entity == null || center == null) return entity.Pos.XYZ.Clone();

            var world = Sapi.World;
            var ba = world?.BlockAccessor;
            var ct = world?.CollisionTester;
            if (ba == null || ct == null) return entity.Pos.XYZ.Clone();

            var selBox = trap?.SelectionBox ?? entity.SelectionBox;
            if (selBox == null) return entity.Pos.XYZ.Clone();

            int dim = entity.Pos.Dimension;
            double r = Math.Max(0.5, range);
            double minR = r * Math.Clamp(minRingFrac, 0f, 0.95f);

            int attemptCount = Math.Max(1, tries);
            for (int attempt = 0; attempt < attemptCount; attempt++)
            {
                double ang = world.Rand.NextDouble() * Math.PI * 2.0;
                double dist = minR + world.Rand.NextDouble() * (r - minR);

                double x = center.X + Math.Cos(ang) * dist;
                double z = center.Z + Math.Sin(ang) * dist;

                int baseY = (int)Math.Round(center.Y);
                var basePos = new BlockPos((int)Math.Floor(x), baseY, (int)Math.Floor(z), dim);

                if (TryFindFreeSpotNearForSelectionBox(selBox, basePos, requireSolidGround, out var found))
                {
                    if (avoidPositions != null && avoidPositions.Count > 0 && minSeparation > 0f)
                    {
                        bool tooClose = false;
                        for (int i = 0; i < avoidPositions.Count; i++)
                        {
                            var p = avoidPositions[i];
                            if (p == null) continue;

                            double dx = found.X - p.X;
                            double dz = found.Z - p.Z;
                            if (dx * dx + dz * dz < (double)minSeparation * (double)minSeparation)
                            {
                                tooClose = true;
                                break;
                            }
                        }

                        if (tooClose)
                        {
                            continue;
                        }
                    }

                    return found;
                }
            }

            return entity.Pos.XYZ.Clone();
        }

        private bool TryFindFreeSpotNearForSelectionBox(Cuboidf selBox, BlockPos basePos, bool requireSolidGround, out Vec3d pos)
        {
            pos = null;
            if (Sapi == null || entity == null || basePos == null || selBox == null) return false;

            var world = Sapi.World;
            var ba = world?.BlockAccessor;
            if (ba == null) return false;

            var ct = world.CollisionTester;
            if (ct == null) return false;

            for (int dy = 0; dy <= FindFreeSpotMaxVerticalSteps; dy++)
            {
                int y = basePos.Y + dy;
                var testPos = new Vec3d(basePos.X + 0.5, y + 1.0, basePos.Z + 0.5);

                bool colliding = ct.IsColliding(ba, selBox, testPos, alsoCheckTouch: false);

                if (colliding) continue;

                if (requireSolidGround)
                {
                    var belowPos = basePos.Copy();
                    belowPos.Y = basePos.Y + dy - 1;
                    var below = ba.GetBlock(belowPos);
                    if (below == null) continue;
                    if (!below.SideSolid[BlockFacing.UP.Index]) continue;
                }

                pos = new Vec3d(testPos.X, testPos.Y - 1.0, testPos.Z);
                return true;
            }

            for (int dy = 1; dy <= FindFreeSpotMaxVerticalSteps; dy++)
            {
                int y = basePos.Y - dy;
                if (y < 0) break;

                var testPos = new Vec3d(basePos.X + 0.5, y + 1.0, basePos.Z + 0.5);

                bool colliding = ct.IsColliding(ba, selBox, testPos, alsoCheckTouch: false);

                if (colliding) continue;

                if (requireSolidGround)
                {
                    var belowPos = basePos.Copy();
                    belowPos.Y = basePos.Y - dy - 1;
                    var below = ba.GetBlock(belowPos);
                    if (below == null) continue;
                    if (!below.SideSolid[BlockFacing.UP.Index]) continue;
                }

                pos = new Vec3d(testPos.X, testPos.Y - 1.0, testPos.Z);
                return true;
            }

            return false;
        }

        private void ApplyTrapFlags(Entity trap, Stage stage)
        {
            if (trap?.WatchedAttributes == null || stage == null) return;

            trap.WatchedAttributes.SetBool(TrapFlagKey, true);
            trap.WatchedAttributes.MarkPathDirty(TrapFlagKey);

            trap.WatchedAttributes.SetLong(TrapOwnerIdKey, entity.EntityId);
            trap.WatchedAttributes.MarkPathDirty(TrapOwnerIdKey);

            trap.WatchedAttributes.SetLong(TrapExplodeAtMsKey, Sapi.World.ElapsedMilliseconds + Math.Max(MinFuseMs, stage.fuseMs));
            trap.WatchedAttributes.MarkPathDirty(TrapExplodeAtMsKey);

            trap.WatchedAttributes.SetFloat(TrapRadiusKey, stage.explosionRadius);
            trap.WatchedAttributes.MarkPathDirty(TrapRadiusKey);

            trap.WatchedAttributes.SetFloat(TrapDamageKey, stage.explosionDamage);
            trap.WatchedAttributes.MarkPathDirty(TrapDamageKey);

            trap.WatchedAttributes.SetInt(TrapDamageTierKey, stage.damageTier);
            trap.WatchedAttributes.MarkPathDirty(TrapDamageTierKey);

            trap.WatchedAttributes.SetString(TrapDamageTypeKey, stage.damageType);
            trap.WatchedAttributes.MarkPathDirty(TrapDamageTypeKey);

            if (!string.IsNullOrWhiteSpace(stage.explodeSound))
            {
                trap.WatchedAttributes.SetString(TrapExplodeSoundKey, stage.explodeSound);
                trap.WatchedAttributes.MarkPathDirty(TrapExplodeSoundKey);
            }

            trap.WatchedAttributes.SetFloat(TrapExplodeSoundRangeKey, stage.explodeSoundRange);
            trap.WatchedAttributes.MarkPathDirty(TrapExplodeSoundRangeKey);

            trap.WatchedAttributes.SetFloat(TrapExplodeSoundVolumeKey, stage.explodeSoundVolume);
            trap.WatchedAttributes.MarkPathDirty(TrapExplodeSoundVolumeKey);

            trap.WatchedAttributes.SetBool("alegacyvsquest:bossclone:invulnerable", stage.trapInvulnerable);
            trap.WatchedAttributes.MarkPathDirty("alegacyvsquest:bossclone:invulnerable");

            trap.WatchedAttributes.SetBool("showHealthbar", false);
            trap.WatchedAttributes.MarkPathDirty("showHealthbar");
        }

        private bool IsTrapEntity()
        {
            return entity?.WatchedAttributes?.GetBool(TrapFlagKey, false) ?? false;
        }

        private void TickTrap()
        {
            if (Sapi == null || entity == null) return;
            if (!entity.Alive) return;

            long now = Sapi.World.ElapsedMilliseconds;

            var wa = entity.WatchedAttributes;
            long explodeAt = wa.GetLong(TrapExplodeAtMsKey, 0);
            long ownerId = wa.GetLong(TrapOwnerIdKey, 0);

            if (explodeAt <= 0) return;

            if (now < explodeAt)
            {
                // While far from explosion, avoid doing expensive work each tick.
                // Only validate owner occasionally.
                if (ownerId <= 0)
                {
                    Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    return;
                }

                if (lastTrapOwnerCheckMs == 0 || now - lastTrapOwnerCheckMs >= TrapOwnerCheckIntervalMs)
                {
                    lastTrapOwnerCheckMs = now;
                    var ownerCheck = Sapi.World.GetEntityById(ownerId);
                    if (ownerCheck == null || !ownerCheck.Alive)
                    {
                        Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    }
                }

                return;
            }

            // Explosion time reached: read remaining parameters and attempt explode.
            float radius = wa.GetFloat(TrapRadiusKey, 0f);
            float damage = wa.GetFloat(TrapDamageKey, 0f);
            int tier = wa.GetInt(TrapDamageTierKey, 0);
            string dmgTypeStr = wa.GetString(TrapDamageTypeKey, null);
            string explodeSound = wa.GetString(TrapExplodeSoundKey, null);
            float explodeSoundRange = wa.GetFloat(TrapExplodeSoundRangeKey, 0f);
            float explodeSoundVolume = wa.GetFloat(TrapExplodeSoundVolumeKey, 1f);

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

            if (!TryExplode(owner as EntityAgent, radius, damage, tier, dmgTypeStr, explodeSound, explodeSoundRange, explodeSoundVolume))
            {
                wa.SetLong(TrapExplodeAtMsKey, now + ExplodeRetryDelayMs);
                wa.MarkPathDirty(TrapExplodeAtMsKey);

                return;
            }

            Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
        }

        private bool TryExplode(EntityAgent owner, float radius, float damage, int tier, string dmgTypeStr, string explodeSound, float explodeSoundRange, float explodeSoundVolume)
        {
            if (Sapi == null || entity == null) return false;
            if (radius <= 0f) return false;
            if (damage <= 0f) return false;

            EnumDamageType dmgType = EnumDamageType.PiercingAttack;
            if (!string.IsNullOrWhiteSpace(dmgTypeStr) && Enum.TryParse(dmgTypeStr, ignoreCase: true, out EnumDamageType parsed))
            {
                dmgType = parsed;
            }

            if (!string.IsNullOrWhiteSpace(explodeSound))
            {
                // Prevent stacking when several traps explode at nearly the same time.
                long limiterEntityId = owner?.EntityId ?? 0;

                string limiterKey = $"ent:{limiterEntityId}:{explodeSound}";
                if (!BossBehaviorUtils.ShouldPlaySoundLimited(limiterKey, ExplodeSoundLimiterMs))
                {
                    explodeSound = null;
                }

                if (!string.IsNullOrWhiteSpace(explodeSound))
                {
                    var soundLoc = AssetLocation.Create(explodeSound, "game")?.WithPathPrefixOnce("sounds/");
                    if (soundLoc != null)
                    {
                        float range = explodeSoundRange > 0f ? explodeSoundRange : 24f;
                        float volume = explodeSoundVolume;
                        if (volume <= 0f) volume = 1f;
                        float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                        Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, range, volume);
                    }
                }
            }

            int dim = entity.Pos.Dimension;
            var center = new Vec3d(entity.Pos.X, entity.Pos.Y + dim * 32768.0, entity.Pos.Z);
            var entities = Sapi.World.GetEntitiesAround(center, radius, radius, e => e is EntityPlayer);
            if (entities == null) return false;

            for (int i = 0; i < entities.Length; i++)
            {
                if (entities[i] is not EntityPlayer plr) continue;
                if (!plr.Alive) continue;
                if (plr.Pos.Dimension != entity.Pos.Dimension) continue;

                plr.ReceiveDamage(new DamageSource()
                {
                    Source = EnumDamageSource.Entity,
                    SourceEntity = owner,
                    Type = dmgType,
                    DamageTier = tier,
                    KnockbackStrength = 0f
                }, damage);
            }

            int smokeMin = Math.Max(65, (int)(radius * 56f));
            int smokeMax = Math.Max(smokeMin + 25, (int)(radius * 98f));

            SimpleParticleProperties smoke = new SimpleParticleProperties(
                smokeMin, smokeMax,
                ColorUtil.ToRgba(140, 30, 30, 30),
                new Vec3d(),
                new Vec3d(radius, Math.Max(1.0, radius * 0.6), radius),
                new Vec3f(-0.6f, 0.05f, -0.6f),
                new Vec3f(0.6f, 0.35f, 0.6f),
                0.125f,
                -0.06f,
                0.5f,
                0.5f,
                EnumParticleModel.Quad
            );
            smoke.MinPos = center.AddCopy(-radius * 0.5, -0.25, -radius * 0.5);
            Sapi.World.SpawnParticles(smoke);

            SimpleParticleProperties flash = new SimpleParticleProperties(
                14, 24,
                ColorUtil.ToRgba(255, 255, 220, 120),
                new Vec3d(),
                new Vec3d(Math.Max(0.5, radius * 0.35), 0.25, Math.Max(0.5, radius * 0.35)),
                new Vec3f(-0.2f, 0.2f, -0.2f),
                new Vec3f(0.2f, 0.6f, 0.2f),
                0.03f,
                0f,
                0.12f,
                0.06f,
                EnumParticleModel.Quad
            );
            flash.MinPos = center.AddCopy(-0.1, 0.2, -0.1);
            Sapi.World.SpawnParticles(flash);

            return true;
        }

        private void CancelPending()
        {
            UnregisterCallbackSafe(ref callbackId);
            callbackId = 0;
        }

        private void TryPlaySound(Stage stage)
        {
            if (Sapi == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.sound)) return;

            // Prevent stacking when multiple traps are spawned in the same moment.
            if (!BossBehaviorUtils.ShouldPlaySoundLimited(entity, stage.sound, SpawnSoundLimiterMs)) return;

            AssetLocation soundLoc = AssetLocation.Create(stage.sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            float volume = stage.soundVolume;
            if (volume <= 0f) volume = 1f;
            float range = stage.soundRange > 0f ? stage.soundRange : DefaultSoundRange;

            if (stage.soundStartMs > 0)
            {
                Sapi.Event.RegisterCallback(_ =>
                {
                    if (entity == null || !entity.Alive) return;
                    float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                    Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, range, volume);
                }, stage.soundStartMs);
            }
            else
            {
                if (entity == null || !entity.Alive) return;
                float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, range, volume);
            }
        }
    }
}
