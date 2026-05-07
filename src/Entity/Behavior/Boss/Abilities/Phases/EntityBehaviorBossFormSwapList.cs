using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossFormSwapList : BossAbilityBase
    {
        private const string StageKey = "alegacyvsquest:bossformswaplist:stage";
        private const string AnchorKeyPrefix = "alegacyvsquest:spawner:";
        private const string TargetIdKey = "alegacyvsquest:killaction:targetid";

        protected override string CooldownKey => "alegacyvsquest:bossformswaplist:lastStartMs";
        protected override bool UseHealthBasedStages() => false;
        protected override bool RequiresTarget() => true; // Can be configured per stage
        protected override int CheckIntervalMs => 500;

        private const string CloneFlagKey = "alegacyvsquest:bossplayerclone";
        private const string CloneOwnerIdKey = "alegacyvsquest:bossplayerclone:ownerid";

        private class Stage : BossAbilityStage
        {
            public string entityCode;
            public bool requireTarget;
            public bool keepHealthFraction;
            public string sound;
            public float soundRange;
            public int soundStartMs;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                entityCode = json["entityCode"].AsString(null);
                requireTarget = json["requireTarget"].AsBool(true);
                keepHealthFraction = json["keepHealthFraction"].AsBool(true);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(24f);
                soundStartMs = json["soundStartMs"].AsInt(0);
            }
        }

        private List<Stage> stages = new List<Stage>();

        public EntityBehaviorBossFormSwapList(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossformswaplist";

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

            if (stage.requireTarget && !entity.WatchedAttributes.GetBool(BossBehaviorUtils.HasTargetKey, false))
                return;

            MarkCooldownStart();
            entity.WatchedAttributes.SetInt(StageKey, stageIndex + 1);
            entity.WatchedAttributes.MarkPathDirty(StageKey);

            if (!TryGetHealthFraction(out float frac)) frac = 1f;
            TrySwapForm(stage, frac);
        }

        protected override void StopAbility()
        {
            // No-op: form swap is instant, no cleanup needed
        }

        protected override bool OnAbilityTick(float dt)
        {
            return false; // Instant activation, no ongoing state
        }

        private void TryRebindPlayerClones(long oldOwnerId, long newOwnerId)
        {
            if (Sapi == null || oldOwnerId <= 0 || newOwnerId <= 0) return;

            var loaded = Sapi.World?.LoadedEntities;
            if (loaded == null) return;

            foreach (var e in loaded.Values)
            {
                if (e == null || !e.Alive) continue;

                var wa = e.WatchedAttributes;
                if (wa == null) continue;

                if (!wa.GetBool(CloneFlagKey, false)) continue;

                long owner = wa.GetLong(CloneOwnerIdKey, 0);
                if (owner != oldOwnerId) continue;

                wa.SetLong(CloneOwnerIdKey, newOwnerId);
                wa.MarkPathDirty(CloneOwnerIdKey);
            }
        }


        private void TrySwapForm(Stage stage, float healthFraction)
        {
            if (stage == null || string.IsNullOrWhiteSpace(stage.entityCode)) return;

            long oldEntityId = entity?.EntityId ?? 0;
            var type = Sapi.World.GetEntityType(new AssetLocation(stage.entityCode));
            if (type == null) return;

            Entity newEntity = Sapi.World.ClassRegistry.CreateEntity(type);
            if (newEntity == null) return;

            CopyTargetId(newEntity);
            CopyAnchor(newEntity);
            CopyLeash(newEntity);

            Vec3d pos = new Vec3d(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
            int dim = entity.Pos.Dimension;
            float yaw = entity.Pos.Yaw;

            newEntity.Pos.SetPosWithDimension(new Vec3d(pos.X, pos.Y + dim * 32768.0, pos.Z));
            newEntity.Pos.Yaw = yaw;
            newEntity.Pos.SetFrom(newEntity.Pos);

            TryPlaySwapSound(stage);

            Sapi.World.SpawnEntity(newEntity);

            // Notify BossHuntSystem so state machine and timers stay in sync
            try
            {
                string targetId = newEntity.WatchedAttributes?.GetString(TargetIdKey, null);
                if (!string.IsNullOrWhiteSpace(targetId))
                {
                    var bh = Sapi.ModLoader?.GetModSystem<BossHuntSystem>();
                    bh?.OnBossRebirthComplete(targetId, newEntity);
                }
            }
            catch
            {
            }

            // Keep any existing player clones alive by rebinding them to the newly spawned boss entity.
            TryRebindPlayerClones(oldEntityId, newEntity.EntityId);

            if (stage.keepHealthFraction)
            {
                float fraction = GameMath.Clamp(healthFraction, 0.05f, 1f);
                RegisterCallbackTracked(_ =>
                {
                    TryApplyHealthFraction(newEntity, fraction);
                }, 1);
            }

            Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
        }

        private void TryPlaySwapSound(Stage stage)
        {
            if (Sapi == null || stage == null || string.IsNullOrWhiteSpace(stage.sound)) return;
            float range = stage.soundRange > 0f ? stage.soundRange : 24f;

            AssetLocation soundLoc = AssetLocation.Create(stage.sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            if (stage.soundStartMs > 0)
            {
                RegisterCallbackTracked(_ =>
                {
                    if (entity == null || !entity.Alive) return;
                    float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                    Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, range, 1f);
                }, stage.soundStartMs);
            }
            else
            {
                if (entity == null || !entity.Alive) return;
                float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, range, 1f);
            }
        }

        private void TryApplyHealthFraction(Entity target, float fraction)
        {
            if (target == null) return;
            if (!target.TryGetHealth(out var healthTree, out float cur, out float maxHealth)) return;

            float newHealth = Math.Max(1f, maxHealth * fraction);
            if (healthTree != null)
            {
                healthTree.SetFloat("currenthealth", newHealth);
                target.WatchedAttributes.MarkPathDirty("health");
            }
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

        private void CopyLeash(Entity newEntity)
        {
            if (newEntity?.WatchedAttributes == null || entity?.WatchedAttributes == null) return;

            float leashRange = entity.WatchedAttributes.GetFloat(EntityBehaviorQuestTarget.LeashRangeKey, float.NaN);
            if (!float.IsNaN(leashRange))
            {
                newEntity.WatchedAttributes.SetFloat(EntityBehaviorQuestTarget.LeashRangeKey, leashRange);
                newEntity.WatchedAttributes.MarkPathDirty(EntityBehaviorQuestTarget.LeashRangeKey);
            }

            float outOfCombatLeashRange = entity.WatchedAttributes.GetFloat(EntityBehaviorQuestTarget.OutOfCombatLeashRangeKey, float.NaN);
            if (!float.IsNaN(outOfCombatLeashRange))
            {
                newEntity.WatchedAttributes.SetFloat(EntityBehaviorQuestTarget.OutOfCombatLeashRangeKey, outOfCombatLeashRange);
                newEntity.WatchedAttributes.MarkPathDirty(EntityBehaviorQuestTarget.OutOfCombatLeashRangeKey);
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
