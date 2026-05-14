using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    /// <summary>
    /// Blood Price: boss heals for a % of damage dealt. After healing, enters vulnerability window.
    /// The more it healed, the longer the vulnerability. Creates a risk/reward dynamic.
    /// </summary>
    public class EntityBehaviorBossBloodPrice : EntityBehavior
    {
        private float healPercent = 0.3f;
        private float minVulnMs = 1500f;
        private float maxVulnMs = 4000f;
        private float healThreshold = 5f; // minimum heal amount to trigger vulnerability
        private float cooldownMs = 12000f;

        private float accumulatedHeal;
        private double lastHealTriggerMs;
        private bool vulnerableFromHeal;
        private double vulnEndMs;

        public bool IsVulnerableFromHeal => vulnerableFromHeal;

        public EntityBehaviorBossBloodPrice(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossbloodprice";

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);
            healPercent = typeAttributes["healPercent"].AsFloat(0.3f);
            minVulnMs = typeAttributes["minVulnMs"].AsFloat(1500f);
            maxVulnMs = typeAttributes["maxVulnMs"].AsFloat(4000f);
            healThreshold = typeAttributes["healThreshold"].AsFloat(5f);
            cooldownMs = typeAttributes["cooldownMs"].AsFloat(12000f);
        }

        /// <summary>
        /// Called when boss deals damage. Heals boss and potentially triggers vulnerability.
        /// </summary>
        public void OnBossDealtDamage(float damageDealt)
        {
            if (entity.Api?.Side != EnumAppSide.Server) return;
            if (!entity.Alive || damageDealt <= 0) return;

            double nowMs = entity.World.ElapsedMilliseconds;
            if (nowMs - lastHealTriggerMs < cooldownMs) return;

            float healAmount = damageDealt * healPercent;
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

            // Spawn green heal particles
            SpawnHealParticles(healAmount);

            // Check if accumulated heal triggers vulnerability
            if (accumulatedHeal >= healThreshold)
            {
                TriggerVulnerability();
            }
        }

        private void TriggerVulnerability()
        {
            double nowMs = entity.World.ElapsedMilliseconds;
            lastHealTriggerMs = nowMs;

            // Calculate vulnerability duration based on how much was healed
            float ratio = Math.Min(1f, accumulatedHeal / (healThreshold * 3f));
            float vulnDuration = minVulnMs + (maxVulnMs - minVulnMs) * ratio;

            vulnerableFromHeal = true;
            vulnEndMs = nowMs + vulnDuration;
            accumulatedHeal = 0;

            // Notify vulnerability window behavior if present
            var vulnBehavior = entity.GetBehavior<EntityBehaviorBossVulnerabilityWindow>();
            vulnBehavior?.OnAbilityCompleted("bossbloodprice");
        }

        public void OnGameTick(float dt)
        {
            if (!vulnerableFromHeal) return;

            double nowMs = entity.World.ElapsedMilliseconds;
            if (nowMs >= vulnEndMs)
            {
                vulnerableFromHeal = false;
            }
        }

        private void SpawnHealParticles(float amount)
        {
            var pos = entity.Pos;
            int count = Math.Min(10, (int)(amount * 2));

            entity.World.SpawnParticles(new SimpleParticleProperties(
                count, count + 3, ColorUtil.ToRgba(180, 50, 200, 50),
                new Vec3d(pos.X - 0.5, pos.Y + 0.5, pos.Z - 0.5),
                new Vec3d(pos.X + 0.5, pos.Y + 1.5, pos.Z + 0.5),
                new Vec3f(0, 0.1f, 0), new Vec3f(0, 0.3f, 0),
                0.6f, -0.02f, 0.1f, 0.25f));
        }
    }
}
