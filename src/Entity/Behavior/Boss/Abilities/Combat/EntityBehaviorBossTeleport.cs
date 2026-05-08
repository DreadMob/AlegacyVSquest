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
    public class EntityBehaviorBossTeleport : BossAbilityBase
    {
        private const string CloneOwnerIdKey = "alegacyvsquest:bossclone:ownerid";
        private const string CloneFlagKey = "alegacyvsquest:bossclone";

        protected override string CooldownKey => "alegacyvsquest:bossteleport:lastStartMs";
        protected override int CheckIntervalMs => 200;

        private class TeleportStage : BossAbilityStage
        {
            public float minRadius;
            public float maxRadius;
            public int tries;

            public int windupMs;
            public string windupAnimation;
            public string arriveAnimation;

            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;

            public bool requireSolidGround;

            public bool teleportClones;
            public bool swapWithClones;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                minRadius = json["minRadius"].AsFloat(3f);
                maxRadius = json["maxRadius"].AsFloat(7f);
                tries = json["tries"].AsInt(10);
                windupMs = json["windupMs"].AsInt(250);
                windupAnimation = json["windupAnimation"].AsString(null);
                arriveAnimation = json["arriveAnimation"].AsString(null);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(24f);
                soundStartMs = json["soundStartMs"].AsInt(0);
                soundVolume = json["soundVolume"].AsFloat(1f);
                requireSolidGround = json["requireSolidGround"].AsBool(true);
                teleportClones = json["teleportClones"].AsBool(false);
                swapWithClones = json["swapWithClones"].AsBool(false);

                // Validation
                if (minRadius < 0f) minRadius = 0f;
                if (maxRadius < minRadius) maxRadius = minRadius;
                if (tries <= 0) tries = 1;
                if (windupMs < 0) windupMs = 0;
                if (soundVolume <= 0f) soundVolume = 1f;
            }
        }

        private List<TeleportStage> stages = new List<TeleportStage>();
        private long teleportCallbackId;

        public EntityBehaviorBossTeleport(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossteleport";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<TeleportStage>(attributes);
        }

        protected override int GetStageCount() => stages.Count;

        protected override object GetStage(int index) => stages[index];

        protected override float GetStageHealthThreshold(object stage) => ((TeleportStage)stage).whenHealthRelBelow;

        protected override float GetStageCooldown(object stage) => ((TeleportStage)stage).cooldownSeconds;

        protected override float GetMaxTargetRange(object stage) => ((TeleportStage)stage).maxTargetRange;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (target == null || stageObj is not TeleportStage stage) return;
            StartTeleport(stage, stageIndex, target.Pos.XYZ);
        }

        protected override bool ShouldCheckAbility() => !IsAbilityActive;

        protected override void StopAbility()
        {
            CancelPending();
        }

        protected override bool OnAbilityTick(float dt) => IsAbilityActive;

        private void StartTeleport(TeleportStage stage, int stageIndex, Vec3d targetPos)
        {
            if (Sapi == null || entity == null || stage == null || targetPos == null) return;

            MarkCooldownStart();
            SetAbilityActive(true);

            BossBehaviorUtils.StopAiAndFreeze(entity);

            TryPlaySound(stage);
            TryPlayAnimation(stage.windupAnimation);

            int delay = Math.Max(0, stage.windupMs);
            UnregisterCallbackSafe(ref teleportCallbackId);
            teleportCallbackId = Sapi.Event.RegisterCallback(_ =>
            {
                DoTeleport(stage, targetPos);
                TryPlayAnimation(stage.arriveAnimation);

                SetAbilityActive(false);
                teleportCallbackId = 0;

            }, delay);
        }

        private void DoTeleport(TeleportStage stage, Vec3d pos)
        {
            if (Sapi == null || entity == null || pos == null) return;

            int dim = entity.Pos.Dimension;

            var clones = (stage != null && (stage.teleportClones || stage.swapWithClones)) ? FindClones() : null;
            Vec3d bossOldPos = new Vec3d(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);

            if (stage?.swapWithClones == true && clones != null && clones.Count > 0)
            {
                var chosen = clones[Sapi.World.Rand.Next(clones.Count)];
                var clonePos = new Vec3d(chosen.Pos.X, chosen.Pos.Y, chosen.Pos.Z);

                TeleportEntity(chosen, bossOldPos, dim);
                TeleportEntity(entity, clonePos, dim);

                clones.Remove(chosen);

                if (stage.teleportClones && clones.Count > 0)
                {
                    TeleportClonesAround(clones, entity.Pos.XYZ, stage, dim);
                }

                return;
            }

            TeleportEntity(entity, pos, dim);

            if (stage?.teleportClones == true && clones != null && clones.Count > 0)
            {
                TeleportClonesAround(clones, entity.Pos.XYZ, stage, dim);
            }
        }

        private void TeleportEntity(Entity target, Vec3d pos, int dim)
        {
            if (target == null || pos == null) return;

            target.IsTeleport = true;

            target.Pos.SetPosWithDimension(new Vec3d(pos.X, pos.Y + dim * 32768.0, pos.Z));
            target.Pos.SetFrom(target.Pos);

            // Safety: sometimes even a validated spot can become colliding (chunk load timing / selection box quirks).
            // Attempt a quick post-teleport nudge to a nearby free spot.
            var world = Sapi?.World;
            var ba = world?.BlockAccessor;
            var ct = world?.CollisionTester;
            var selBox = target.SelectionBox;
            if (ba != null && ct != null && selBox != null)
            {
                var tmpPos = new Vec3d(target.Pos.X, target.Pos.Y + dim * 32768.0, target.Pos.Z);
                bool colliding = ct.IsColliding(ba, selBox, tmpPos, alsoCheckTouch: false);
                if (colliding)
                {
                    var bp = new BlockPos((int)Math.Floor(target.Pos.X), (int)Math.Floor(target.Pos.Y), (int)Math.Floor(target.Pos.Z), dim);
                    if (TryFindFreeSpotNear(bp, requireSolidGround: true, out var found))
                    {
                        target.Pos.SetPosWithDimension(new Vec3d(found.X, found.Y + dim * 32768.0, found.Z));
                        target.Pos.SetFrom(target.Pos);
                    }
                }
            }

            target.Pos.Motion.Set(0, 0, 0);
        }

        private List<Entity> FindClones()
        {
            var result = new List<Entity>();
            if (Sapi == null || entity == null) return result;

            var loaded = Sapi.World?.LoadedEntities;
            if (loaded == null) return result;

            long ownerId = entity.EntityId;
            foreach (var entry in loaded)
            {
                var e = entry.Value;
                if (e == null || !e.Alive) continue;
                if (e.Pos.Dimension != entity.Pos.Dimension) continue;

                var wa = e.WatchedAttributes;
                if (wa == null) continue;
                if (!wa.GetBool(CloneFlagKey, false)) continue;
                long cloneOwner = wa.GetLong(CloneOwnerIdKey, 0);
                if (cloneOwner != ownerId) continue;

                result.Add(e);
            }

            return result;
        }

        private void TeleportClonesAround(List<Entity> clones, Vec3d center, TeleportStage stage, int dim)
        {
            if (clones == null || clones.Count == 0 || stage == null || center == null) return;

            for (int i = 0; i < clones.Count; i++)
            {
                var clone = clones[i];
                if (clone == null || !clone.Alive) continue;

                if (TryFindTeleportPos(stage, center, dim, out var pos))
                {
                    TeleportEntity(clone, pos, dim);
                }
            }
        }

        private void CancelPending()
        {
            UnregisterCallbackSafe(ref teleportCallbackId);
            teleportCallbackId = 0;
        }

        private bool TryFindTeleportPos(TeleportStage stage, Vec3d targetPos, int dim, out Vec3d pos)
        {
            pos = null;
            if (Sapi == null || entity == null || targetPos == null || stage == null) return false;

            double minR = Math.Max(0.0, stage.minRadius);
            double maxR = Math.Max(minR, stage.maxRadius);
            if (maxR <= 0.01) maxR = 0.01;

            var world = Sapi.World;
            if (world == null) return false;

            var ba = world.BlockAccessor;
            if (ba == null) return false;

            int tries = Math.Max(1, stage.tries);

            for (int attempt = 0; attempt < tries; attempt++)
            {
                double ang = world.Rand.NextDouble() * Math.PI * 2.0;
                double dist = minR + world.Rand.NextDouble() * (maxR - minR);

                double x = targetPos.X + Math.Cos(ang) * dist;
                double z = targetPos.Z + Math.Sin(ang) * dist;

                int baseY = (int)Math.Round(targetPos.Y);

                var tmp = new BlockPos((int)Math.Floor(x), baseY, (int)Math.Floor(z), dim);

                if (TryFindFreeSpotNear(tmp, stage.requireSolidGround, out var found))
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

        private void TryPlaySound(TeleportStage stage)
        {
            if (Sapi == null || stage == null || string.IsNullOrWhiteSpace(stage.sound)) return;

            // Prevent sound stacking when teleport/clone logic triggers multiple times close together.
            if (!BossBehaviorUtils.ShouldPlaySoundLimited(entity, stage.sound, 500)) return;

            AssetLocation soundLoc = AssetLocation.Create(stage.sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            float volume = stage.soundVolume;
            if (volume <= 0f) volume = 1f;
            volume *= 1.5f;

            float range = stage.soundRange > 0f ? stage.soundRange : 32f;

            // Apply pitch multiplier from entity attributes
            float pitchMult = 1f;
            try
            {
                pitchMult = entity?.Properties?.Attributes?["vsquestSoundPitchMul"].AsFloat(1f) ?? 1f;
            }
            catch { }
            if (pitchMult <= 0f || Math.Abs(pitchMult - 1f) < 0.0001f) pitchMult = 1f;

            if (stage.soundStartMs > 0)
            {
                Sapi.Event.RegisterCallback(_ =>
                {
                    float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                    if (pitchMult != 1f) pitch *= pitchMult;
                    Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, range, volume);
                }, stage.soundStartMs);
            }
            else
            {
                float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                if (pitchMult != 1f) pitch *= pitchMult;
                Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, range, volume);
            }
        }
    }
}
