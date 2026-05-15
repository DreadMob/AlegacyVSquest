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
    /// Blood Price: reactive ability that triggers when boss deals damage.
    /// Boss heals for a % of damage dealt. After healing past threshold, enters vulnerability window.
    /// Never auto-activates — OnBossDealtDamage is called externally.
    /// </summary>
    public class EntityBehaviorBossBloodPrice : BossAbilityBase
    {
        private const string LastBloodPriceKey = "alegacyvsquest:bossbloodprice:lastMs";
        protected override string CooldownKey => LastBloodPriceKey;

        private class Stage : BossAbilityStage
        {
            public float healPercent;
            public float minVulnMs;
            public float maxVulnMs;
            public float healThreshold;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                healPercent = json["healPercent"].AsFloat(0.3f);
                minVulnMs = json["minVulnMs"].AsFloat(1500f);
                maxVulnMs = json["maxVulnMs"].AsFloat(4000f);
                healThreshold = json["healThreshold"].AsFloat(5f);
            }
        }

        private List<Stage> stages = new();
        private float accumulatedHeal;

        public EntityBehaviorBossBloodPrice(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossbloodprice";

        protected override bool UsePeriodicTick() => false;
        protected override bool RequiresTarget() => false;
        protected override bool ShouldCheckAbility() => false;

        protected override void InitializeStages(JsonObject attributes) { stages = ParseStages<Stage>(attributes); }
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            // Not used — this is a reactive ability
        }

        /// <summary>
        /// Called when boss deals damage. Heals boss and potentially triggers vulnerability.
        /// </summary>
        public void OnBossDealtDamage(float damageDealt)
        {
            if (Sapi == null || !entity.Alive || damageDealt <= 0) return;
            if (stages.Count == 0) return;
            if (IsBossClone) return;

            // Check cooldown
            if (!CooldownSystem.IsCooldownReady(CooldownKey, GetCurrentStageCooldown()))
                return;

            // Find appropriate stage for current health
            if (!entity.TryGetHealthFraction(out float frac)) return;
            var (stageObj, _) = FindStageForHealth(frac);
            if (stageObj is not Stage stage) return;

            float healAmount = damageDealt * stage.healPercent;
            accumulatedHeal += healAmount;

            // Actually heal the boss
            var healthTree = entity.WatchedAttributes.GetTreeAttribute("health");
            if (healthTree != null)
            {
                float currentHp = healthTree.GetFloat("currenthealth", 0);
                float maxHp = healthTree.GetFloat("maxhealth", currentHp);
                float newHp = Math.Min(maxHp, currentHp + healAmount);
                healthTree.SetFloat("currenthealth", newHp);
                entity.WatchedAttributes.MarkPathDirty("health");
            }

            // Blood/heal particles
            ParticleUtils.SpawnEntityAura(Sapi, entity, ParticleUtils.Colors.Blood, 8, 0.35f, 0.6f);
            ParticleUtils.SpawnSpiral(Sapi, entity.Pos.XYZ, 0.8f, 2f, ParticleUtils.Colors.Blood, 16, 0.2f);

            // Check if accumulated heal triggers vulnerability
            if (accumulatedHeal >= stage.healThreshold)
            {
                TriggerVulnerability(stage);
            }
        }

        private void TriggerVulnerability(Stage stage)
        {
            MarkCooldownStart();
            accumulatedHeal = 0;

            // Notify vulnerability window behavior if present
            var vulnBehavior = entity.GetBehavior<EntityBehaviorBossVulnerabilityWindow>();
            vulnBehavior?.OnAbilityCompleted("bossbloodprice");
        }

        private float GetCurrentStageCooldown()
        {
            if (stages.Count == 0) return 0f;
            if (!entity.TryGetHealthFraction(out float frac)) return stages[0].cooldownSeconds;
            var (stageObj, _) = FindStageForHealth(frac);
            if (stageObj is Stage stage) return stage.cooldownSeconds;
            return stages[0].cooldownSeconds;
        }

        protected override void StopAbility()
        {
            accumulatedHeal = 0;
        }
    }
}
