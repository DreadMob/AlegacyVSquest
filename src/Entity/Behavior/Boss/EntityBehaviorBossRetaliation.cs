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
    /// Retaliation: reactive ability that triggers on receiving damage.
    /// Boss absorbs incoming damage over a window, then releases it as an AoE burst.
    /// Uses periodic tick to check window timeout.
    /// </summary>
    public class EntityBehaviorBossRetaliation : BossAbilityBase
    {
        private const string LastRetaliationKey = "alegacyvsquest:bossretaliation:lastMs";
        protected override string CooldownKey => LastRetaliationKey;

        private class Stage : BossAbilityStage
        {
            public float damageThreshold;
            public float retaliationMultiplier;
            public float retaliationRadius;
            public float windowDurationSec;
            public string sound;
            public float soundRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                damageThreshold = json["damageThreshold"].AsFloat(16f);
                retaliationMultiplier = json["retaliationMultiplier"].AsFloat(0.6f);
                retaliationRadius = json["retaliationRadius"].AsFloat(6f);
                windowDurationSec = json["windowDurationSec"].AsFloat(4f);
                sound = json["sound"].AsString("effect/reverbhit");
                soundRange = json["soundRange"].AsFloat(32f);
            }
        }

        private List<Stage> stages = new();

        private bool absorbing;
        private long absorbStartMs;
        private float absorbedDamage;
        private int activeStageIndex;

        public bool IsAbsorbing => absorbing;
        public float AbsorbProgress => absorbing && activeStageIndex >= 0 && activeStageIndex < stages.Count
            ? Math.Min(1f, absorbedDamage / stages[activeStageIndex].damageThreshold) : 0f;

        public EntityBehaviorBossRetaliation(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossretaliation";

        protected override bool UsePeriodicTick() => true;
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

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (Sapi == null || damage <= 0 || !entity.Alive) return;
            if (stages.Count == 0) return;
            if (IsBossClone) return;

            // Auto-activate on first damage after cooldown
            if (!absorbing)
            {
                // Find appropriate stage for current health
                if (!entity.TryGetHealthFraction(out float frac)) return;
                var (stageObj, stageIndex) = FindStageForHealth(frac);
                if (stageObj is not Stage stage) return;

                if (!CooldownSystem.IsCooldownReady(CooldownKey, stage.cooldownSeconds)) return;

                StartAbsorbing(stageIndex);
            }

            if (absorbing)
            {
                absorbedDamage += damage;

                // Absorb particles
                ParticleUtils.SpawnEntityAura(Sapi, entity, ParticleUtils.Colors.Arcane, 4, 0.2f + AbsorbProgress * 0.3f, 0.4f);

                // Check threshold
                if (activeStageIndex >= 0 && activeStageIndex < stages.Count)
                {
                    if (absorbedDamage >= stages[activeStageIndex].damageThreshold)
                    {
                        Retaliate();
                    }
                }
            }
        }

        protected override void OnPeriodicTick(float dt)
        {
            if (Sapi == null || !absorbing) return;
            if (activeStageIndex < 0 || activeStageIndex >= stages.Count)
            {
                absorbing = false;
                return;
            }

            var stage = stages[activeStageIndex];
            long nowMs = Sapi.World.ElapsedMilliseconds;

            // Persistent warning aura while absorbing — red/orange pulsing particles
            float intensity = 0.3f + AbsorbProgress * 0.7f; // grows as damage accumulates
            var pos = entity.Pos;
            Sapi.World.SpawnParticles(new SimpleParticleProperties(
                minQuantity: 3 + (int)(AbsorbProgress * 8), maxQuantity: 6 + (int)(AbsorbProgress * 12),
                color: ColorUtil.ToRgba((int)(150 + AbsorbProgress * 100), 255, (int)(80 - AbsorbProgress * 50), 20),
                minPos: new Vec3d(pos.X - 0.8, pos.Y + 0.2, pos.Z - 0.8),
                maxPos: new Vec3d(pos.X + 0.8, pos.Y + 1.8, pos.Z + 0.8),
                minVelocity: new Vec3f(-0.02f, 0.05f, -0.02f),
                maxVelocity: new Vec3f(0.02f, 0.15f, 0.02f),
                lifeLength: 0.5f + intensity * 0.5f,
                gravityEffect: -0.02f,
                minSize: 0.15f + intensity * 0.2f,
                maxSize: 0.3f + intensity * 0.3f
            ));

            // Window expired without reaching threshold — release what we have
            if (nowMs - absorbStartMs >= stage.windowDurationSec * 1000)
            {
                if (absorbedDamage > 0)
                {
                    Retaliate();
                }
                else
                {
                    absorbing = false;
                    entity.WatchedAttributes.SetBool("alegacyvsquest:retaliation:absorbing", false);
                    entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:retaliation:absorbing");
                }
            }
        }

        private void StartAbsorbing(int stageIndex)
        {
            absorbing = true;
            absorbStartMs = Sapi.World.ElapsedMilliseconds;
            absorbedDamage = 0;
            activeStageIndex = stageIndex;

            entity.WatchedAttributes.SetBool("alegacyvsquest:retaliation:absorbing", true);
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:retaliation:absorbing");

            // Warning: visual burst + sound when absorption starts
            ParticleUtils.SpawnEntityAura(Sapi, entity, ParticleUtils.Colors.ArcaneBright, 12, 0.4f, 0.6f);
            TryPlaySound("effect/reverbhit", 24f, 0, 0.3f);
        }

        private void Retaliate()
        {
            absorbing = false;
            MarkCooldownStart();

            entity.WatchedAttributes.SetBool("alegacyvsquest:retaliation:absorbing", false);
            entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:retaliation:absorbing");

            if (activeStageIndex < 0 || activeStageIndex >= stages.Count) return;
            var stage = stages[activeStageIndex];

            float retDamage = absorbedDamage * stage.retaliationMultiplier;
            absorbedDamage = 0;

            if (retDamage < 1f) return;

            // AoE damage to all players in radius
            foreach (var p in Sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe == null || !pe.Alive) continue;
                if (pe.Pos.Dimension != entity.Pos.Dimension) continue;

                double dist = Math.Sqrt(pe.Pos.SquareDistanceTo(entity.Pos.XYZ));
                if (dist <= stage.retaliationRadius)
                {
                    // Damage falls off with distance
                    float falloff = 1f - (float)(dist / stage.retaliationRadius) * 0.5f;
                    pe.ReceiveDamage(new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.Injury
                    }, retDamage * falloff);
                }
            }

            // Shockwave on burst
            ParticleUtils.SpawnShockwave(Sapi, entity.Pos.XYZ, stage.retaliationRadius, ParticleUtils.Colors.Arcane, 30, 0.5f);

            // Sounds
            TryPlaySound(stage.sound, stage.soundRange);
            TryPlaySound("environment/thunder1", 48f);
        }

        protected override void StopAbility()
        {
            absorbing = false;
            absorbedDamage = 0;
            entity?.WatchedAttributes?.SetBool("alegacyvsquest:retaliation:absorbing", false);
        }
    }
}
