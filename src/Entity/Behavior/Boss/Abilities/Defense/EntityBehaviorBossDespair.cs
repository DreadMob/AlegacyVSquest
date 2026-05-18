using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossDespair : BossAbilityBase
    {
        private const string DespairStageKey = "alegacyvsquest:bossdespairstage";

        protected override string CooldownKey => "alegacyvsquest:bossdespair:lastStartMs";
        protected override bool UseHealthBasedStages() => true;
        protected override bool RequiresTarget() => false;
        protected override int CheckIntervalMs => 500;

        private class Stage : BossAbilityStage
        {
            public float durationMultiplier;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                durationMultiplier = json["despairDurationMultiplier"].AsFloat(3f);
                if (durationMultiplier <= 0f) durationMultiplier = 3f;
            }
        }
        private List<Stage> stages = new List<Stage>();
        private long soundLoopListenerId;
        private float lockedYaw;
        private bool yawLocked;
        private long healListenerId;
        private int currentDespairStage = 0;

        public EntityBehaviorBossDespair(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossdespair";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
            currentDespairStage = entity.WatchedAttributes.GetInt(DespairStageKey, 0);
        }

        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;
        protected override bool ShouldCheckAbility() => !IsAbilityActive && entity.AnimManager != null;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;
            MarkCooldownStart();

            int nextStage = currentDespairStage == 0 ? 1 : 2;
            currentDespairStage = nextStage;

            entity.WatchedAttributes.SetInt(DespairStageKey, nextStage);
            entity.WatchedAttributes.MarkPathDirty(DespairStageKey);

            SetAbilityActive(true);
            BossBehaviorUtils.StopAiAndFreeze(entity);
            BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);
            entity.AnimManager.StartAnimation("despair");

            UnregisterGameTickListenerSafe(ref soundLoopListenerId);
            UnregisterGameTickListenerSafe(ref healListenerId);

            soundLoopListenerId = Sapi.Event.RegisterGameTickListener(_ =>
            {
                float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;
                Sapi.World.PlaySoundAt(new AssetLocation("sounds/creature/shiver/aggro"), entity, null, pitch, 1f);

                // Despair aura particles
                ParticleUtils.SpawnEntityAura(Sapi, entity, ParticleUtils.Colors.Shadow, 8, 0.5f, 0.7f);
            }, 1000);

            healListenerId = Sapi.Event.RegisterGameTickListener(_ =>
            {
                HealDuringDespair();
            }, 500);

            int baseSeconds = (int)(Sapi.World.Rand.NextDouble() * 3.0 + 3.0);
            int durationMs = (int)(baseSeconds * 1000 * stage.durationMultiplier);

            Sapi.Event.RegisterCallback(_ =>
            {
                entity.AnimManager.StopAnimation("despair");

                Sapi.Event.RegisterCallback(__ =>
                {
                    StopDespairEffects();
                    Unfreeze();
                }, 200);

            }, durationMs);
        }

        private void Unfreeze()
        {
            entity.Pos.Motion.Set(0, 0, 0);
        }

        private void HealDuringDespair()
        {
            if (!BossBehaviorUtils.TryGetHealth(entity, out var healthTree, out float curHealth, out float maxHealth)) return;

            float newHealth = Math.Min(maxHealth, curHealth + 2.0f);
            healthTree.SetFloat("currenthealth", newHealth);
            entity.WatchedAttributes.MarkPathDirty("health");
        }

        protected override void StopAbility()
        {
            StopDespairEffects();
        }

        protected override bool OnAbilityTick(float dt)
        {
            if (!IsAbilityActive) return false;
            BossBehaviorUtils.ApplyRotationLock(entity, ref yawLocked, ref lockedYaw);
            return true;
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            StopDespairEffects();
            base.OnEntityDeath(damageSourceForDeath);
        }

        private void StopDespairEffects()
        {
            SetAbilityActive(false);
            yawLocked = false;

            UnregisterGameTickListenerSafe(ref soundLoopListenerId);
            UnregisterGameTickListenerSafe(ref healListenerId);
        }

    }
}
