using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VsQuest
{
    public class EntityBehaviorBossAntiCheese : EntityBehaviorBossBase, IWorldIntersectionSupplier
    {
        private const string LastStartMsKey = "alegacyvsquest:bossanticheese:lastStartMs";

        private class Stage
        {
            public float whenHealthRelBelow;

            public int checkIntervalMs;
            public float cooldownSeconds;

            public float searchRange;

            public float farRange;
            public float farSeconds;

            public float noLosSeconds;

            public float minRadius;
            public float maxRadius;
            public int tries;
            public bool requireSolidGround;

            public int windupMs;
            public string windupAnimation;
            public string arriveAnimation;

            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;
        }

        private readonly List<Stage> stages = new List<Stage>();

        private long lastCheckMs;
        private float farSecondsAcc;
        private float noLosSecondsAcc;

        private long lastLosCheckMs;
        private bool cachedHasLos;

        private long callbackId;
        private bool pending;

        private readonly BlockSelection blockSel = new BlockSelection();
        private readonly EntitySelection entitySel = new EntitySelection();
        private readonly Vec3d rayTraceFrom = new Vec3d();
        private readonly Vec3d rayTraceTo = new Vec3d();

        public Vec3i MapSize => entity.World.BlockAccessor.MapSize;
        public IBlockAccessor blockAccessor => entity.World.BlockAccessor;

        public EntityBehaviorBossAntiCheese(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossanticheese";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            stages.Clear();
            try
            {
                foreach (var stageObj in attributes["stages"].AsArray())
                {
                    if (stageObj == null || !stageObj.Exists) continue;

                    var stage = new Stage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),

                        checkIntervalMs = stageObj["checkIntervalMs"].AsInt(250),
                        cooldownSeconds = stageObj["cooldownSeconds"].AsFloat(12f),

                        searchRange = stageObj["searchRange"].AsFloat(40f),

                        farRange = stageObj["farRange"].AsFloat(18f),
                        farSeconds = stageObj["farSeconds"].AsFloat(2.5f),

                        noLosSeconds = stageObj["noLosSeconds"].AsFloat(2.0f),

                        minRadius = stageObj["minRadius"].AsFloat(3f),
                        maxRadius = stageObj["maxRadius"].AsFloat(7f),
                        tries = stageObj["tries"].AsInt(10),
                        requireSolidGround = stageObj["requireSolidGround"].AsBool(true),

                        windupMs = stageObj["windupMs"].AsInt(250),
                        windupAnimation = stageObj["windupAnimation"].AsString(null),
                        arriveAnimation = stageObj["arriveAnimation"].AsString(null),

                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(24f),
                        soundStartMs = stageObj["soundStartMs"].AsInt(0),
                        soundVolume = stageObj["soundVolume"].AsFloat(1f)
                    };

                    if (stage.checkIntervalMs < 50) stage.checkIntervalMs = 50;
                    if (stage.cooldownSeconds < 0f) stage.cooldownSeconds = 0f;

                    if (stage.searchRange < 1f) stage.searchRange = 1f;

                    if (stage.farRange < 0f) stage.farRange = 0f;
                    if (stage.farSeconds < 0f) stage.farSeconds = 0f;
                    if (stage.noLosSeconds < 0f) stage.noLosSeconds = 0f;

                    if (stage.minRadius < 0f) stage.minRadius = 0f;
                    if (stage.maxRadius < stage.minRadius) stage.maxRadius = stage.minRadius;
                    if (stage.tries <= 0) stage.tries = 1;

                    if (stage.windupMs < 0) stage.windupMs = 0;
                    if (stage.soundVolume <= 0f) stage.soundVolume = 1f;

                    stages.Add(stage);
                }
            }
            catch
            {
            }
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (Sapi == null || entity == null) return;
            if (entity.Api?.Side != EnumAppSide.Server) return;
            if (stages.Count == 0) return;

            if (!entity.Alive)
            {
                ResetState();
                CancelPending();
                return;
            }

            if (pending) return;

            if (!TryGetHealthFraction(out float frac)) return;

            int stageIndex = -1;
            for (int i = 0; i < stages.Count; i++)
            {
                if (frac <= stages[i].whenHealthRelBelow)
                {
                    stageIndex = i;
                }
            }

            if (stageIndex < 0 || stageIndex >= stages.Count) return;
            var stage = stages[stageIndex];

            long now = Sapi.World.ElapsedMilliseconds;
            if (lastCheckMs != 0 && now - lastCheckMs < stage.checkIntervalMs) return;
            lastCheckMs = now;

            if (!TryFindTarget(stage, out var target, out float dist))
            {
                ResetState();
                return;
            }

            bool far = stage.farRange > 0f && dist >= stage.farRange;
            if (far)
            {
                farSecondsAcc += stage.checkIntervalMs / 1000f;
            }
            else
            {
                farSecondsAcc = 0f;
            }

            if (stage.noLosSeconds > 0f)
            {
                bool hasLos;
                try
                {
                    int losCheckIntervalMs = Math.Max(500, stage.checkIntervalMs);
                    if (lastLosCheckMs == 0 || now - lastLosCheckMs >= losCheckIntervalMs)
                    {
                        cachedHasLos = HasLineOfSight(target);
                        lastLosCheckMs = now;
                    }

                    hasLos = cachedHasLos;
                }
                catch
                {
                    hasLos = false;
                }

                if (!hasLos)
                {
                    noLosSecondsAcc += stage.checkIntervalMs / 1000f;
                }
                else
                {
                    noLosSecondsAcc = 0f;
                }
            }
            else
            {
                noLosSecondsAcc = 0f;
            }

            bool farTrigger = stage.farSeconds > 0f && farSecondsAcc >= stage.farSeconds;
            bool noLosTrigger = stage.noLosSeconds > 0f && noLosSecondsAcc >= stage.noLosSeconds;

            if (!farTrigger && !noLosTrigger) return;

            if (!BossBehaviorUtils.IsCooldownReady(Sapi, entity, LastStartMsKey, stage.cooldownSeconds)) return;

            if (!TryFindTeleportPos(stage, target.Pos.XYZ, entity.Pos.Dimension, out var tpPos))
            {
                ResetState();
                return;
            }

            StartTeleport(stage, tpPos);
            ResetState();
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            ResetState();
            CancelPending();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            ResetState();
            CancelPending();
            base.OnEntityDespawn(despawn);
        }

        private void ResetState()
        {
            farSecondsAcc = 0f;
            noLosSecondsAcc = 0f;
            lastLosCheckMs = 0;
            cachedHasLos = false;
        }

        private void CancelPending()
        {
            UnregisterCallbackSafe(ref callbackId);
            pending = false;
        }

        private bool TryFindTarget(Stage stage, out EntityPlayer target, out float dist)
        {
            target = null;
            dist = 0f;

            if (Sapi == null || entity == null || stage == null) return false;

            double range = Math.Max(1.0, stage.searchRange);
            try
            {
                var own = entity.Pos.XYZ;
                float frange = (float)range;
                var found = Sapi.World.GetNearestEntity(own, frange, frange, e => e is EntityPlayer) as EntityPlayer;
                if (found == null || !found.Alive) return false;
                if (found.Pos.Dimension != entity.Pos.Dimension) return false;

                target = found;
                dist = (float)found.Pos.DistanceTo(entity.Pos);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool HasLineOfSight(EntityPlayer target)
        {
            if (entity == null || target == null) return false;

            try
            {
                rayTraceFrom.Set(entity.Pos);
                rayTraceFrom.Y += 1.0 / 32.0;
                rayTraceTo.Set(target.Pos);
                rayTraceTo.Y += 1.0 / 32.0;

                BlockSelection bs = null;
                EntitySelection es = null;

                entity.World.RayTraceForSelection(this, rayTraceFrom, rayTraceTo, ref bs, ref es);
                if (bs == null) return true;

                rayTraceFrom.Y += entity.SelectionBox.Y2 * 7f / 16f;
                rayTraceTo.Y += target.SelectionBox.Y2 * 7f / 16f;
                entity.World.RayTraceForSelection(this, rayTraceFrom, rayTraceTo, ref bs, ref es);
                if (bs == null) return true;

                rayTraceFrom.Y += entity.SelectionBox.Y2 * 7f / 16f;
                rayTraceTo.Y += target.SelectionBox.Y2 * 7f / 16f;
                entity.World.RayTraceForSelection(this, rayTraceFrom, rayTraceTo, ref bs, ref es);
                return bs == null;
            }
            catch
            {
                return false;
            }
        }

        private void StartTeleport(Stage stage, Vec3d pos)
        {
            if (Sapi == null || entity == null || stage == null || pos == null) return;

            BossBehaviorUtils.MarkCooldownStart(Sapi, entity, LastStartMsKey);

            pending = true;

            entity.StopAiAndFreeze();

            TryPlaySound(stage.sound, stage.soundRange, stage.soundStartMs, stage.soundVolume);
            TryPlayAnimation(stage.windupAnimation);

            int delay = Math.Max(0, stage.windupMs);
            UnregisterCallbackSafe(ref callbackId);
            callbackId = Sapi.Event.RegisterCallback(_ =>
            {
                try
                {
                    TeleportEntity(entity, pos, entity.Pos.Dimension);
                    TryPlayAnimation(stage.arriveAnimation);
                }
                catch
                {
                }

                pending = false;
                callbackId = 0;

            }, delay);
        }

        private void TeleportEntity(Entity target, Vec3d pos, int dim)
        {
            if (target == null || pos == null) return;

            try
            {
                target.IsTeleport = true;
            }
            catch
            {
            }

            target.Pos.SetPosWithDimension(new Vec3d(pos.X, pos.Y + dim * 32768.0, pos.Z));
            target.Pos.SetFrom(target.Pos);

            try
            {
                target.Pos.Motion.Set(0, 0, 0);
            }
            catch
            {
            }
        }

        private bool TryFindTeleportPos(Stage stage, Vec3d center, int dim, out Vec3d pos)
        {
            pos = null;
            if (Sapi == null || entity == null || stage == null || center == null) return false;

            double minR = Math.Max(0.0, stage.minRadius);
            double maxR = Math.Max(minR, stage.maxRadius);
            if (maxR <= 0.01) maxR = 0.01;

            var world = Sapi.World;
            var ba = world?.BlockAccessor;
            if (ba == null) return false;

            int tries = Math.Max(1, stage.tries);

            for (int attempt = 0; attempt < tries; attempt++)
            {
                double ang = world.Rand.NextDouble() * Math.PI * 2.0;
                double dist = minR + world.Rand.NextDouble() * (maxR - minR);

                double x = center.X + Math.Cos(ang) * dist;
                double z = center.Z + Math.Sin(ang) * dist;

                int baseY = (int)Math.Round(center.Y);
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

            for (int dy = 0; dy <= 4; dy++)
            {
                int y = basePos.Y + dy;
                var testPos = new Vec3d(basePos.X + 0.5, y + 1.0, basePos.Z + 0.5);

                bool colliding;
                try
                {
                    colliding = ct.IsColliding(ba, selBox, testPos, alsoCheckTouch: false);
                }
                catch
                {
                    colliding = true;
                }

                if (colliding) continue;

                if (requireSolidGround)
                {
                    try
                    {
                        var belowPos = basePos.Copy();
                        belowPos.Y = basePos.Y + dy - 1;
                        var below = ba.GetBlock(belowPos);
                        if (below == null) continue;
                        if (!below.SideSolid[BlockFacing.UP.Index]) continue;
                    }
                    catch
                    {
                        continue;
                    }
                }

                pos = new Vec3d(testPos.X, testPos.Y - 1.0, testPos.Z);
                return true;
            }

            for (int dy = 1; dy <= 4; dy++)
            {
                int y = basePos.Y - dy;
                if (y < 0) break;

                var testPos = new Vec3d(basePos.X + 0.5, y + 1.0, basePos.Z + 0.5);

                bool colliding;
                try
                {
                    colliding = ct.IsColliding(ba, selBox, testPos, alsoCheckTouch: false);
                }
                catch
                {
                    colliding = true;
                }

                if (colliding) continue;

                if (requireSolidGround)
                {
                    try
                    {
                        var belowPos = basePos.Copy();
                        belowPos.Y = basePos.Y - dy - 1;
                        var below = ba.GetBlock(belowPos);
                        if (below == null) continue;
                        if (!below.SideSolid[BlockFacing.UP.Index]) continue;
                    }
                    catch
                    {
                        continue;
                    }
                }

                pos = new Vec3d(testPos.X, testPos.Y - 1.0, testPos.Z);
                return true;
            }

            return false;
        }

        public Block GetBlock(BlockPos pos)
        {
            return entity.World.BlockAccessor.GetBlock(pos);
        }

        public Cuboidf[] GetBlockIntersectionBoxes(BlockPos pos)
        {
            return entity.World.BlockAccessor.GetBlock(pos).GetCollisionBoxes(entity.World.BlockAccessor, pos);
        }

        public bool IsValidPos(BlockPos pos)
        {
            return entity.World.BlockAccessor.IsValidPos(pos);
        }

        public Entity[] GetEntitiesAround(Vec3d position, float horRange, float vertRange, ActionConsumable<Entity> matches = null)
        {
            return Array.Empty<Entity>();
        }
    }
}
