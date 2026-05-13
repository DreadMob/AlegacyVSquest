using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossAshFloor : BossAbilityBase
    {
        protected override string CooldownKey => "alegacyvsquest:bossashfloor:lastStartMs";

        private class Stage : BossAbilityStage
        {
            public float minRadius;
            public float maxRadius;
            public int tries;

            public int blockCount;
            public int durationMs;

            public int tickIntervalMs;
            public float damage;
            public int damageTier;
            public string damageType;

            public float victimWalkSpeedMult;
            public bool disableJump;
            public bool disableShift;

            public int windupMs;
            public string windupAnimation;

            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                minRadius = json["minRadius"].AsFloat(0f);
                maxRadius = json["maxRadius"].AsFloat(6f);
                tries = json["tries"].AsInt(14);
                blockCount = json["blockCount"].AsInt(8);
                durationMs = json["durationMs"].AsInt(12000);
                tickIntervalMs = json["tickIntervalMs"].AsInt(1000);
                damage = json["damage"].AsFloat(0f);
                damageTier = json["damageTier"].AsInt(0);
                damageType = json["damageType"].AsString("Acid");
                victimWalkSpeedMult = json["victimWalkSpeedMult"].AsFloat(0.35f);
                disableJump = json["disableJump"].AsBool(true);
                disableShift = json["disableShift"].AsBool(true);
                windupMs = json["windupMs"].AsInt(0);
                windupAnimation = json["windupAnimation"].AsString(null);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(24f);
                soundStartMs = json["soundStartMs"].AsInt(0);
                soundVolume = json["soundVolume"].AsFloat(1f);

                // Validation
                if (minRadius < 0f) minRadius = 0f;
                if (maxRadius < minRadius) maxRadius = minRadius;
                if (tries <= 0) tries = 1;
                if (blockCount <= 0) blockCount = 1;
                if (durationMs <= 0) durationMs = 500;
                if (tickIntervalMs < 50) tickIntervalMs = 50;
                if (damage < 0f) damage = 0f;
                if (damageTier < 0) damageTier = 0;
                victimWalkSpeedMult = GameMath.Clamp(victimWalkSpeedMult, 0f, 1f);
                if (windupMs < 0) windupMs = 0;
                if (soundVolume <= 0f) soundVolume = 1f;
            }
        }

        private List<Stage> stages = new List<Stage>();

        private long callbackId;
        
        public EntityBehaviorBossAshFloor(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossashfloor";

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

        protected override void StopAbility()
        {
            CancelPending();
        }

        protected override bool OnAbilityTick(float dt)
        {
            return IsAbilityActive;
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

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;
            StartAshFloor(stage, stageIndex, target);
        }

        private void StartAshFloor(Stage stage, int stageIndex, EntityPlayer target)
        {
            if (Sapi == null || entity == null || stage == null || target == null) return;

            MarkCooldownStart();

            BossBehaviorUtils.StopAiAndFreeze(entity);
            TryPlaySound(stage);
            TryPlayAnimation(stage.windupAnimation);

            int delay = Math.Max(0, stage.windupMs);
            UnregisterCallbackSafe(ref callbackId);
            callbackId = Sapi.Event.RegisterCallback(_ =>
            {
                PlaceAsh(stage, target);
                SetAbilityActive(false);
                callbackId = 0;
            }, delay);
        }

        private void PlaceAsh(Stage stage, EntityPlayer target)
        {
            if (Sapi == null || entity == null || stage == null || target == null) return;

            // Ash floor placement visual
            ParticleUtils.SpawnFalling(Sapi, target.Pos.XYZ, stage.maxRadius, 3f, ParticleUtils.Colors.SmokeDark, 20, 0.3f);

            var ba = Sapi.World?.BlockAccessor;
            if (ba == null) return;

            Block ashBlock = Sapi.World.GetBlock(new AssetLocation("alegacyvsquest:ashfloor"));
            if (ashBlock == null || ashBlock.IsMissing) return;

            int dim = entity.Pos.Dimension;
            var center = target.Pos.XYZ;

            long now = Sapi.World.ElapsedMilliseconds;
            long despawnAt = now + Math.Max(250, stage.durationMs);

            var used = new HashSet<string>(StringComparer.Ordinal);

            int tries = Math.Max(1, stage.tries);
            int count = Math.Max(1, stage.blockCount);

            for (int i = 0; i < count; i++)
            {
                if (TryFindPlacePos(ba, center, dim, stage.minRadius, stage.maxRadius, tries, used, out var pos))
                {
                    TryPlaceOne(ba, ashBlock, pos, despawnAt, stage);
                    TryPlaceOne(ba, ashBlock, pos.AddCopy(1, 0, 0), despawnAt, stage);
                    TryPlaceOne(ba, ashBlock, pos.AddCopy(0, 0, 1), despawnAt, stage);
                    TryPlaceOne(ba, ashBlock, pos.AddCopy(1, 0, 1), despawnAt, stage);
                }
            }
        }

        private void TryPlaceOne(IBlockAccessor ba, Block ashBlock, BlockPos pos, long despawnAtMs, Stage stage)
        {
            if (Sapi == null || ba == null || ashBlock == null || pos == null || stage == null) return;

            if (!ba.IsValidPos(pos)) return;

            var at = ba.GetBlock(pos);
            var below = ba.GetBlock(pos.DownCopy());

            if (at == null || below == null) return;
            if (at.Replaceable < 6000) return;
            if (!below.SideSolid[BlockFacing.UP.Index]) return;

            ba.RemoveBlockEntity(pos);
            ba.SetBlock(ashBlock.BlockId, pos);

            if (ashBlock.EntityClass != null)
            {
                ba.SpawnBlockEntity(ashBlock.EntityClass, pos);
                var be = ba.GetBlockEntity(pos) as BlockEntityAshFloor;
                be?.Arm(entity.EntityId, despawnAtMs, stage.tickIntervalMs, stage.victimWalkSpeedMult);
            }
        }

        private bool TryFindPlacePos(IBlockAccessor ba, Vec3d center, int dim, float minRadius, float maxRadius, int tries, HashSet<string> used, out BlockPos pos)
        {
            pos = null;
            if (Sapi == null || ba == null || center == null) return false;

            double minR = Math.Max(0.0, minRadius);
            double maxR = Math.Max(minR, maxRadius);
            if (maxR <= 0.01) maxR = 0.01;

            int baseY = (int)Math.Round(center.Y);

            for (int attempt = 0; attempt < tries; attempt++)
            {
                double ang = Sapi.World.Rand.NextDouble() * Math.PI * 2.0;
                double dist = minR + Sapi.World.Rand.NextDouble() * (maxR - minR);

                int x = (int)Math.Floor(center.X + Math.Cos(ang) * dist);
                int z = (int)Math.Floor(center.Z + Math.Sin(ang) * dist);

                for (int dy = 3; dy >= -3; dy--)
                {
                    int y = baseY + dy;
                    if (y < 1) continue;

                    var p = new BlockPos(x, y, z, dim);
                    if (!ba.IsValidPos(p)) continue;

                    Block at = ba.GetBlock(p);
                    Block below = ba.GetBlock(new BlockPos(x, y - 1, z, dim));

                    if (at == null || below == null) continue;
                    if (at.Replaceable < 6000) continue;
                    if (!below.SideSolid[BlockFacing.UP.Index]) continue;

                    string key = $"{x},{y},{z},{dim}";
                    if (used != null && used.Contains(key)) continue;
                    used?.Add(key);

                    pos = p;
                    return true;
                }
            }

            return false;
        }

        private void CancelPending()
        {
            UnregisterCallbackSafe(ref callbackId);
            callbackId = 0;
        }

        private void TryPlaySound(Stage stage)
        {
            if (Sapi == null || stage == null) return;
            TryPlaySound(stage.sound, stage.soundRange, stage.soundStartMs, stage.soundVolume);
        }
    }
}
