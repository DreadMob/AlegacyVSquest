using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossPassiveRegen : EntityBehavior
    {
        private float regenPerSecond;
        private float outOfCombatRegenMult;
        private EntityBehaviorHealth healthBehavior;
        private int regenIntervalMs = 500;
        private long lastRegenMs;
        private long combatWindowMs = 240000; // 240 seconds (4 min) before out-of-combat regen starts

        public EntityBehaviorBossPassiveRegen(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            regenPerSecond = attributes["regenPerSecond"].AsFloat(25f);
            outOfCombatRegenMult = attributes["outOfCombatRegenMult"].AsFloat(0f);
            regenIntervalMs = attributes["regenIntervalMs"].AsInt(500);
            combatWindowMs = attributes["combatWindowMs"].AsInt(240000);
            healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();
        }

        private bool IsInCombat()
        {
            try
            {
                var wa = entity.WatchedAttributes;
                if (wa == null) return false;

                long lastDamageMs = wa.GetLong(EntityBehaviorBossCombatMarker.BossCombatLastDamageMsKey, 0);
                if (lastDamageMs == 0) return false;

                long nowMs = entity.World.ElapsedMilliseconds;
                return (nowMs - lastDamageMs) < combatWindowMs;
            }
            catch
            {
                return false;
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            if (entity?.Api?.Side != EnumAppSide.Server) return;
            if (healthBehavior == null || regenPerSecond <= 0) return;

            bool inCombat = IsInCombat();
            float effectiveRegenPerSecond = regenPerSecond;

            // Apply out of combat multiplier when not in combat
            if (!inCombat)
            {
                if (outOfCombatRegenMult <= 0f) return;
                effectiveRegenPerSecond *= outOfCombatRegenMult;
            }

            long nowMs = entity.World.ElapsedMilliseconds;

            // Initialize lastRegenMs on first tick
            if (lastRegenMs == 0)
            {
                lastRegenMs = nowMs;
                return;
            }

            // Only regen every regenIntervalMs to reduce server load
            if (nowMs - lastRegenMs < regenIntervalMs) return;

            lastRegenMs = nowMs;

            try
            {
                if (healthBehavior.Health < healthBehavior.MaxHealth && healthBehavior.Health > 0)
                {
                    float regenAmount = effectiveRegenPerSecond * (regenIntervalMs / 1000f);
                    float oldHealth = healthBehavior.Health;
                    float newHealth = Math.Min(healthBehavior.MaxHealth, oldHealth + regenAmount);
                    healthBehavior.Health = newHealth;
                }
            }
            catch (Exception ex)
            {
                entity.Api?.Logger?.Error("[BossPassiveRegen] {0} error: {1}", entity.Code, ex.Message);
            }
        }

        public override string PropertyName() => "bosspassiveregen";
    }
}
