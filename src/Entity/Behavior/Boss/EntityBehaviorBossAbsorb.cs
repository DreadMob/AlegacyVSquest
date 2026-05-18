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
    /// Boss Absorb: boss activates a shield that absorbs N hits without taking damage.
    /// After absorbing all hits — AoE explosion. If duration expires first — shield drops harmlessly.
    /// Visual: gold/white particle aura while active.
    /// </summary>
    public class EntityBehaviorBossAbsorb : BossAbilityBase
    {
        private const string LastAbsorbKey = "alegacyvsquest:bossabsorb:lastMs";
        protected override string CooldownKey => LastAbsorbKey;

        private class Stage : BossAbilityStage
        {
            public int absorbHitCount;
            public float absorbDurationSec;
            public float explosionDamage;
            public float explosionRadius;
            public string sound;
            public float soundRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                absorbHitCount = json["absorbHitCount"].AsInt(5);
                absorbDurationSec = json["absorbDurationSec"].AsFloat(6f);
                explosionDamage = json["explosionDamage"].AsFloat(20f);
                explosionRadius = json["explosionRadius"].AsFloat(8f);
                sound = json["sound"].AsString("effect/reverbhit");
                soundRange = json["soundRange"].AsFloat(32f);
            }
        }

        private List<Stage> stages = new();
        private bool shieldActive;
        private long shieldStartMs;
        private int hitsAbsorbed;
        private int activeStageIndex;

        public EntityBehaviorBossAbsorb(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossabsorb";

        protected override bool UsePeriodicTick() => true;
        protected override bool ShouldCheckAbility() => false;

        protected override void InitializeStages(JsonObject attributes) { stages = ParseStages<Stage>(attributes); }
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target) { }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (!shieldActive || damage <= 0) return;

            // Absorb the hit — zero out damage
            hitsAbsorbed++;
            damage = 0;

            // Visual feedback per absorbed hit
            if (Sapi != null)
            {
                ParticleUtils.SpawnEntityAura(Sapi, entity, ParticleUtils.Colors.ShieldGold, 6, 0.3f, 0.5f);
                entity.World.PlaySoundAt(new AssetLocation("game:sounds/block/metalhit"), entity.Pos.X, entity.Pos.Y, entity.Pos.Z, null, true, 40, 0.4f);
            }

            // Check if all hits absorbed — explode
            if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
            {
                if (hitsAbsorbed >= stages[activeStageIndex].absorbHitCount)
                {
                    Explode();
                }
            }
        }

        protected override void OnPeriodicTick(float dt)
        {
            if (Sapi == null || !entity.Alive) return;
            if (IsBossClone) return;
            if (stages.Count == 0) return;

            long nowMs = Sapi.World.ElapsedMilliseconds;

            if (shieldActive)
            {
                if (activeStageIndex < 0 || activeStageIndex >= stages.Count)
                {
                    DeactivateShield();
                    return;
                }

                var stage = stages[activeStageIndex];
                double elapsed = (nowMs - shieldStartMs) / 1000.0;

                // Duration expired — shield drops without explosion
                if (elapsed >= stage.absorbDurationSec)
                {
                    DeactivateShield();
                    return;
                }

                // Shield aura particles (gold/white)
                ParticleUtils.SpawnEntityAura(Sapi, entity, ParticleUtils.Colors.ShieldGold, 4, 0.4f, 0.6f);
                return;
            }

            // Try to activate shield
            if (!entity.TryGetHealthFraction(out float frac)) return;
            var (stageObj, stageIndex) = FindStageForHealth(frac);
            if (stageObj is not Stage stg) return;
            if (!CooldownSystem.IsCooldownReady(CooldownKey, stg.cooldownSeconds)) return;

            // Activate
            shieldActive = true;
            shieldStartMs = nowMs;
            hitsAbsorbed = 0;
            activeStageIndex = stageIndex;

            entity.WatchedAttributes.SetBool("alegacyvsquest:absorbing", true);
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:absorbing");

            // Activation burst
            ParticleUtils.SpawnAuraSphere(Sapi, entity.Pos.XYZ, 2f, ParticleUtils.Colors.ShieldGold, 20, 0.5f);
            TryPlaySound("effect/translocate-active", 24f);
        }

        private void Explode()
        {
            if (Sapi == null || activeStageIndex < 0 || activeStageIndex >= stages.Count) return;
            var stage = stages[activeStageIndex];

            DeactivateShield();
            MarkCooldownStart();

            float dmg = ApplyDamageMultiplier(stage.explosionDamage);

            // AoE damage
            foreach (var p in Sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe == null || !pe.Alive) continue;
                if (pe.Pos.Dimension != entity.Pos.Dimension) continue;

                double dist = Math.Sqrt(pe.Pos.SquareDistanceTo(entity.Pos.XYZ));
                if (dist <= stage.explosionRadius)
                {
                    float falloff = 1f - (float)(dist / stage.explosionRadius) * 0.4f;
                    pe.ReceiveDamage(new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.Injury,
                        DamageTier = 3
                    }, dmg * falloff);
                }
            }

            // Explosion effects
            ParticleUtils.SpawnShockwave(Sapi, entity.Pos.XYZ, stage.explosionRadius, ParticleUtils.Colors.ShieldGold, 30, 0.6f);
            ParticleUtils.SpawnFireExplosion(Sapi, entity.Pos.XYZ, 3f, 2);
            TryPlaySound(stage.sound, stage.soundRange);
        }

        private void DeactivateShield()
        {
            shieldActive = false;
            hitsAbsorbed = 0;
            MarkCooldownStart();

            entity.WatchedAttributes.SetBool("alegacyvsquest:absorbing", false);
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:absorbing");
        }

        protected override void StopAbility()
        {
            DeactivateShield();
        }
    }
}
