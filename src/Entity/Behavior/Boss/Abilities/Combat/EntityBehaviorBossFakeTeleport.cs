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
    public class EntityBehaviorBossFakeTeleport : BossAbilityBase
    {
        protected override string CooldownKey => "alegacyvsquest:bossfaketeleport:lastStartMs";
        protected override int CheckIntervalMs => 200;
        private const string FakeFlagKey = "alegacyvsquest:bossfaketeleport:fake";
        private const string FakeOwnerIdKey = "alegacyvsquest:bossfaketeleport:ownerid";
        private const string FakeDespawnAtMsKey = "alegacyvsquest:bossfaketeleport:despawnat";

        private class Stage : BossAbilityStage
        {
            public float minRadius;
            public float maxRadius;
            public int tries;
            public bool requireSolidGround;

            public int windupMs;
            public string windupAnimation;
            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;

            public string fakeEntityCode;
            public int fakeDurationMs;
            public bool fakeInvulnerable;

            public bool teleportBoss;
            public float bossTeleportMinRadius;
            public float bossTeleportMaxRadius;
            public int bossTeleportTries;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                minRadius = json["minRadius"].AsFloat(3f);
                maxRadius = json["maxRadius"].AsFloat(8f);
                tries = json["tries"].AsInt(10);
                requireSolidGround = json["requireSolidGround"].AsBool(true);
                windupMs = json["windupMs"].AsInt(250);
                windupAnimation = json["windupAnimation"].AsString(null);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(24f);
                soundStartMs = json["soundStartMs"].AsInt(0);
                soundVolume = json["soundVolume"].AsFloat(1f);
                fakeEntityCode = json["fakeEntityCode"].AsString(null);
                fakeDurationMs = json["fakeDurationMs"].AsInt(2500);
                fakeInvulnerable = json["fakeInvulnerable"].AsBool(true);
                teleportBoss = json["teleportBoss"].AsBool(false);
                bossTeleportMinRadius = json["bossTeleportMinRadius"].AsFloat(3f);
                bossTeleportMaxRadius = json["bossTeleportMaxRadius"].AsFloat(7f);
                bossTeleportTries = json["bossTeleportTries"].AsInt(10);

                // Validation
                if (minRadius < 0f) minRadius = 0f;
                if (maxRadius < minRadius) maxRadius = minRadius;
                if (tries <= 0) tries = 1;
                if (windupMs < 0) windupMs = 0;
                if (soundVolume <= 0f) soundVolume = 1f;
                if (fakeDurationMs <= 0) fakeDurationMs = 500;
                if (bossTeleportMinRadius < 0f) bossTeleportMinRadius = 0f;
                if (bossTeleportMaxRadius < bossTeleportMinRadius) bossTeleportMaxRadius = bossTeleportMinRadius;
                if (bossTeleportTries <= 0) bossTeleportTries = 1;
            }
        }

        private List<Stage> stages = new List<Stage>();

        private long callbackId;
        private int pendingStageIndex;

        public EntityBehaviorBossFakeTeleport(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossfaketeleport";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        protected override int GetStageCount() => stages.Count;

        protected override object GetStage(int index) => stages[index];

        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;

        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;

        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override float MinTargetRange => 0.75f;

        protected override bool ShouldCheckAbility() => !IsAbilityActive && !IsFakeEntity();

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (target == null || stageObj is not Stage stage) return;
            StartFakeTeleport(stage, stageIndex, target);
        }

        protected override void StopAbility()
        {
            CancelPending();
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            CancelPending();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            CancelPending();
            base.OnEntityDespawn(despawn);
        }

        private void Start(Stage stage, int stageIndex, Vec3d fakePos, Vec3d bossTpPos)
        {
            if (Sapi == null || entity == null || stage == null || fakePos == null) return;

            SetAbilityActive(true);
            pendingStageIndex = stageIndex;

            BossBehaviorUtils.StopAiAndFreeze(entity);

            TryPlaySound(stage);
            TryPlayAnimation(stage.windupAnimation);

            int delay = Math.Max(0, stage.windupMs);
            UnregisterCallbackSafe(ref callbackId);
            callbackId = Sapi.Event.RegisterCallback(_ =>
            {
                SpawnFake(stage, fakePos);
                if (stage.teleportBoss && bossTpPos != null)
                {
                    TeleportEntity(entity, bossTpPos, entity.Pos.Dimension);
                }

                SetAbilityActive(false);
                pendingStageIndex = -1;
                callbackId = 0;

            }, delay);
        }

        private void SpawnFake(Stage stage, Vec3d pos)
        {
            if (Sapi == null || entity == null || stage == null || pos == null) return;
            if (string.IsNullOrWhiteSpace(stage.fakeEntityCode)) return;

            var type = Sapi.World.GetEntityType(new AssetLocation(stage.fakeEntityCode));
            if (type == null) return;

            Entity fake = Sapi.World.ClassRegistry.CreateEntity(type);
            if (fake == null) return;

            ApplyFakeFlags(fake, stage);

            int dim = entity.Pos.Dimension;
            fake.Pos.SetPosWithDimension(new Vec3d(pos.X, pos.Y + dim * 32768.0, pos.Z));
            fake.Pos.Yaw = entity.Pos.Yaw;
            fake.Pos.SetFrom(fake.Pos);

            Sapi.World.SpawnEntity(fake);
        }

        private void ApplyFakeFlags(Entity fake, Stage stage)
        {
            if (fake?.WatchedAttributes == null || stage == null) return;

            fake.WatchedAttributes.SetBool(FakeFlagKey, true);
            fake.WatchedAttributes.MarkPathDirty(FakeFlagKey);

            fake.WatchedAttributes.SetLong(FakeOwnerIdKey, entity.EntityId);
            fake.WatchedAttributes.MarkPathDirty(FakeOwnerIdKey);

            fake.WatchedAttributes.SetLong(FakeDespawnAtMsKey, Sapi.World.ElapsedMilliseconds + Math.Max(250, stage.fakeDurationMs));
            fake.WatchedAttributes.MarkPathDirty(FakeDespawnAtMsKey);

            fake.WatchedAttributes.SetBool("alegacyvsquest:bossclone:invulnerable", stage.fakeInvulnerable);
            fake.WatchedAttributes.MarkPathDirty("alegacyvsquest:bossclone:invulnerable");

            fake.WatchedAttributes.SetBool("showHealthbar", false);
            fake.WatchedAttributes.MarkPathDirty("showHealthbar");
        }

        private bool IsFakeEntity()
        {
            return entity?.WatchedAttributes?.GetBool(FakeFlagKey, false) ?? false;
        }

        private void DespawnIfExpiredOrOwnerMissing()
        {
            if (Sapi == null || entity == null) return;

            var wa = entity.WatchedAttributes;
            long despawnAt = wa.GetLong(FakeDespawnAtMsKey, 0);
            long ownerId = wa.GetLong(FakeOwnerIdKey, 0);

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

            if (despawnAt > 0 && Sapi.World.ElapsedMilliseconds >= despawnAt)
            {
                Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            }
        }

        private void CancelPending()
        {
            if (Sapi != null)
            {
                UnregisterCallbackSafe(ref callbackId);
            }

            pendingStageIndex = -1;
            callbackId = 0;
        }

        private void StartFakeTeleport(Stage stage, int stageIndex, EntityPlayer target)
        {
            if (Sapi == null || entity == null || stage == null || target == null) return;

            MarkCooldownStart();
            SetAbilityActive(true);
            pendingStageIndex = stageIndex;

            BossBehaviorUtils.StopAiAndFreeze(entity);

            TryPlaySound(stage);
            TryPlayAnimation(stage.windupAnimation);

            Vec3d fakePos = null;
            Vec3d bossTpPos = null;
            if (!TryFindPosNear(stage.minRadius, stage.maxRadius, stage.tries, stage.requireSolidGround, entity.Pos.XYZ, entity.Pos.Dimension, out fakePos))
            {
                return;
            }

            if (stage.teleportBoss && !TryFindPosNear(stage.bossTeleportMinRadius, stage.bossTeleportMaxRadius, stage.bossTeleportTries, stage.requireSolidGround, entity.Pos.XYZ, entity.Pos.Dimension, out bossTpPos))
            {
                return;
            }

            Start(stage, stageIndex, fakePos, bossTpPos);
        }

        private bool TryFindPosNear(float minRadius, float maxRadius, int tries, bool requireSolidGround, Vec3d center, int dim, out Vec3d pos)
        {
            pos = null;
            if (Sapi == null || entity == null || center == null) return false;

            double minR = Math.Max(0.0, minRadius);
            double maxR = Math.Max(minR, maxRadius);
            if (maxR <= 0.01) maxR = 0.01;

            var world = Sapi.World;
            var ba = world?.BlockAccessor;
            if (ba == null) return false;

            int attemptCount = Math.Max(1, tries);
            for (int attempt = 0; attempt < attemptCount; attempt++)
            {
                double ang = world.Rand.NextDouble() * Math.PI * 2.0;
                double dist = minR + world.Rand.NextDouble() * (maxR - minR);

                double x = center.X + Math.Cos(ang) * dist;
                double z = center.Z + Math.Sin(ang) * dist;

                int baseY = (int)Math.Round(center.Y);
                var tmp = new BlockPos((int)Math.Floor(x), baseY, (int)Math.Floor(z), dim);

                if (TryFindFreeSpotNear(tmp, requireSolidGround, out var found))
                {
                    pos = found;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindFreeSpotNear(BlockPos basePos, bool requireSolidGround, out Vec3d pos)
        {
            pos = null;
            if (Sapi == null || entity == null || basePos == null) return false;

            var world = Sapi.World;
            var ba = world?.BlockAccessor;
            if (ba == null) return false;

            var ct = world.CollisionTester;
            if (ct == null) return false;

            var selBox = entity.SelectionBox;
            if (selBox == null) return false;

            for (int dy = 0; dy <= 6; dy++)
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

            for (int dy = 1; dy <= 6; dy++)
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

        private void TeleportEntity(Entity target, Vec3d pos, int dim)
        {
            if (target == null || pos == null) return;

            target.IsTeleport = true;
            target.Pos.SetPosWithDimension(new Vec3d(pos.X, pos.Y + dim * 32768.0, pos.Z));
            target.Pos.SetFrom(target.Pos);
            target.Pos.Motion.Set(0, 0, 0);
        }

        private void TryPlaySound(Stage stage)
        {
            if (Sapi == null || stage == null) return;
            TryPlaySound(stage.sound, stage.soundRange, stage.soundStartMs, stage.soundVolume);
        }
    }
}
