using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Base class for boss ability stages with common properties.
    /// </summary>
    public class BossAbilityStage
    {
        public float whenHealthRelBelow = 1f;
        public float cooldownSeconds = 0f;
        public float minTargetRange = 0f;
        public float maxTargetRange = 30f;

        public virtual void FromJson(JsonObject json)
        {
            whenHealthRelBelow = json["whenHealthRelBelow"].AsFloat(1f);
            cooldownSeconds = json["cooldownSeconds"].AsFloat(0f);
            minTargetRange = json["minTargetRange"].AsFloat(0f);
            maxTargetRange = json["maxTargetRange"].AsFloat(30f);
        }
    }

    /// <summary>
    /// Base class for boss abilities with health-based staging.
    /// Provides common patterns: stage selection, cooldown management, target finding, animation/sound playback.
    /// </summary>
    public abstract class BossAbilityBase : EntityBehavior
    {
        protected ICoreServerAPI Sapi { get; private set; }
        protected BossCooldownSystem CooldownSystem { get; private set; }
        protected BossTargetingSystem TargetingSystem { get; private set; }
        protected BossMarkingSystem MarkingSystem { get; private set; }
        
        /// <summary>
        /// Unique key for storing last activation timestamp in WatchedAttributes.
        /// Format: "alegacyvsquest:{abilityname}:lastStartMs"
        /// </summary>
        protected abstract string CooldownKey { get; }

        /// <summary>
        /// Override to parse stages from entity attributes.
        /// </summary>
        protected abstract void InitializeStages(JsonObject attributes);

        /// <summary>
        /// Override to implement ability-specific activation logic.
        /// Called when stage is selected, cooldown is ready, and target is found.
        /// </summary>
        protected abstract void ActivateAbility(object stage, int stageIndex, EntityPlayer target);

        /// <summary>
        /// Override to implement ability-specific cleanup on death/despawn.
        /// </summary>
        protected abstract void StopAbility();

        /// <summary>
        /// Override to implement ability-specific per-tick logic while active.
        /// Return true to continue ticking, false to stop.
        /// </summary>
        protected virtual bool OnAbilityTick(float dt) => false;

        /// <summary>
        /// Override to implement periodic tick logic for auras/rituals.
        /// Called every CheckIntervalMs while UsePeriodicTick() returns true.
        /// </summary>
        protected virtual void OnPeriodicTick(float dt)
        {
        }

        /// <summary>
        /// Override to return true if ability uses health-based stages.
        /// </summary>
        protected virtual bool UseHealthBasedStages() => true;

        /// <summary>
        /// Override to return true if ability requires a target.
        /// </summary>
        protected virtual bool RequiresTarget() => true;

        /// <summary>
        /// Override to return true if ability should be checked.
        /// </summary>
        protected virtual bool ShouldCheckAbility() => true;

        /// <summary>
        /// Override to return true if ability uses periodic tick (always active, like auras/rituals).
        /// Periodic tick abilities bypass the activation/deactivation cycle.
        /// </summary>
        protected virtual bool UsePeriodicTick() => false;

        /// <summary>
        /// Minimum target range for target finding. Override if needed.
        /// </summary>
        protected virtual float MinTargetRange => 0f;

        /// <summary>
        /// Maximum target range for target finding. Override if needed.
        /// </summary>
        protected virtual float MaxTargetRange => 30f;

        /// <summary>
        /// Check interval in milliseconds. Override to throttle checks.
        /// </summary>
        protected virtual int CheckIntervalMs => 200;

        private long lastCheckMs;
        private bool abilityActive;

        protected BossAbilityBase(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            Sapi = entity?.Api as ICoreServerAPI;
            
            if (Sapi != null)
            {
                CooldownSystem = new BossCooldownSystem(Sapi, entity);
                TargetingSystem = new BossTargetingSystem(Sapi, entity);
                MarkingSystem = new BossMarkingSystem(Sapi, entity);
            }
            
            InitializeStages(attributes);
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (Sapi == null || entity == null) return;

            if (!entity.Alive)
            {
                StopAbility();
                return;
            }

            // Periodic tick path for auras/rituals (always active, bypass activation cycle)
            if (UsePeriodicTick())
            {
                long now = Sapi.World.ElapsedMilliseconds;
                if (now - lastCheckMs < CheckIntervalMs) return;
                lastCheckMs = now;

                OnPeriodicTick(dt);
                return;
            }

            // Event-driven path for combat abilities (activation → deactivation cycle)
            if (abilityActive)
            {
                if (!OnAbilityTick(dt))
                {
                    StopAbility();
                    abilityActive = false;
                }
                return;
            }

            // Throttle checks
            long nowCheck = Sapi.World.ElapsedMilliseconds;
            if (nowCheck - lastCheckMs < CheckIntervalMs) return;
            lastCheckMs = nowCheck;

            CheckAbility();
        }

        protected virtual void CheckAbility()
        {
            if (!ShouldCheckAbility()) return;
            if (!entity.Alive) return;

            object stage = null;
            int stageIndex = -1;

            if (UseHealthBasedStages())
            {
                if (!entity.TryGetHealthFraction(out float frac)) return;
                (stage, stageIndex) = FindStageForHealth(frac);
                if (stage == null) return;
            }
            else
            {
                // For non-health-based abilities, use stage 0 if available
                if (GetStageCount() > 0)
                {
                    stage = GetStage(0);
                    stageIndex = 0;
                }
                if (stage == null) return;
            }

            if (!IsCooldownReady(stage)) return;

            EntityPlayer target = null;
            if (RequiresTarget())
            {
                float maxRange = ApplyRangeMultiplier(GetMaxTargetRange(stage));
                if (!TargetingSystem.TryFindTarget(maxRange, MinTargetRange, out target, out float dist)) return;
            }

            if (!CanActivateWithConditions(stage, target)) return;

            abilityActive = true;
            ActivateAbility(stage, stageIndex, target);
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            StopAbility();
            abilityActive = false;
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopAbility();
            abilityActive = false;
            base.OnEntityDespawn(despawn);
        }

        /// <summary>
        /// Find the appropriate stage based on current health fraction.
        /// Returns the stage with highest whenHealthRelBelow that is >= current fraction.
        /// </summary>
        protected (object stage, int index) FindStageForHealth(float healthFraction)
        {
            int stageIndex = -1;
            object selectedStage = null;

            for (int i = 0; i < GetStageCount(); i++)
            {
                var stage = GetStage(i);
                if (stage == null) continue;

                float threshold = GetStageHealthThreshold(stage);
                if (healthFraction <= threshold)
                {
                    stageIndex = i;
                    selectedStage = stage;
                }
            }

            return (selectedStage, stageIndex);
        }

        /// <summary>
        /// Override to return total number of stages.
        /// </summary>
        protected abstract int GetStageCount();

        /// <summary>
        /// Override to return stage at index.
        /// </summary>
        protected abstract object GetStage(int index);

        /// <summary>
        /// Override to return whenHealthRelBelow threshold for a stage.
        /// </summary>
        protected abstract float GetStageHealthThreshold(object stage);

        /// <summary>
        /// Override to return cooldown in seconds for a stage.
        /// </summary>
        protected abstract float GetStageCooldown(object stage);

        /// <summary>
        /// Override to return max target range for a stage.
        /// </summary>
        protected abstract float GetMaxTargetRange(object stage);

        /// <summary>
        /// Check if cooldown is ready for specific stage.
        /// </summary>
        protected bool IsCooldownReady(object stage)
        {
            if (CooldownSystem == null) return false;
            float cooldownSeconds = ApplyCooldownMultiplier(GetStageCooldown(stage));
            return CooldownSystem.IsCooldownReady(CooldownKey, cooldownSeconds);
        }

        /// <summary>
        /// Mark cooldown start time.
        /// </summary>
        protected void MarkCooldownStart()
        {
            CooldownSystem?.MarkCooldownStart(CooldownKey);
        }

        /// <summary>
        /// Play sound with optional delay and volume adjustment from entity attributes.
        /// </summary>
        protected void TryPlaySound(string sound, float range = 24f, int startDelayMs = 0, float volume = 1f)
        {
            if (Sapi == null || string.IsNullOrWhiteSpace(sound)) return;

            AssetLocation soundLoc = AssetLocation.Create(sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            float adjustedVolume = AdjustSoundVolume(soundLoc, sound, volume);
            float finalRange = range > 0f ? range : 32f;

            if (startDelayMs > 0)
            {
                Sapi.Event.RegisterCallback(_ =>
                {
                    try
                    {
                        Sapi.World.PlaySoundAt(soundLoc, entity, null, false, finalRange, adjustedVolume);
                    }
                    catch (Exception ex)
                    {
                        entity?.Api?.Logger?.Error($"[vsquest] Exception in delayed TryPlaySound: {ex}");
                    }
                }, startDelayMs);
            }
            else
            {
                Sapi.World.PlaySoundAt(soundLoc, entity, null, false, finalRange, adjustedVolume);
            }
        }

        /// <summary>
        /// Override to modify sound volume based on entity attributes.
        /// </summary>
        protected virtual float AdjustSoundVolume(AssetLocation soundLoc, string sound, float volume) => volume;

        protected void TryPlayAnimation(string animation)
        {
            if (string.IsNullOrWhiteSpace(animation)) return;
            entity?.AnimManager?.StartAnimation(animation);
        }

        protected void TryStopAnimation(string animation)
        {
            if (string.IsNullOrWhiteSpace(animation)) return;
            entity?.AnimManager?.StopAnimation(animation);
        }

        /// <summary>
        /// Generic helper to parse stages from JsonObject.
        /// </summary>
        protected List<T> ParseStages<T>(JsonObject attributes) where T : BossAbilityStage, new()
        {
            var result = new List<T>();
            try
            {
                var stagesArray = attributes["stages"]?.AsArray();
                if (stagesArray != null)
                {
                    foreach (var stageObj in stagesArray)
                    {
                        if (stageObj == null || !stageObj.Exists) continue;
                        var stage = new T();
                        stage.FromJson(stageObj);
                        result.Add(stage);
                    }
                }
            }
            catch (Exception ex)
            {
                Sapi?.Logger.Error($"[vsquest] Error parsing stages for {PropertyName()}: {ex}");
            }
            return result;
        }

        /// <summary>
        /// Unregister callback safely using BossBehaviorUtils.
        /// </summary>
        protected void UnregisterCallbackSafe(ref long callbackId)
        {
            BossBehaviorUtils.UnregisterCallbackSafe(Sapi, ref callbackId);
        }

        /// <summary>
        /// Unregister game tick listener safely using BossBehaviorUtils.
        /// </summary>
        protected void UnregisterGameTickListenerSafe(ref long listenerId)
        {
            BossBehaviorUtils.UnregisterGameTickListenerSafe(Sapi, ref listenerId);
        }

        /// <summary>
        /// Set ability active state.
        /// </summary>
        protected void SetAbilityActive(bool active)
        {
            abilityActive = active;
        }

        /// <summary>
        /// Check if ability is currently active.
        /// </summary>
        protected bool IsAbilityActive => abilityActive;

        // ========================================
        // ABILITY MODIFIER SYSTEM
        // ========================================

        /// <summary>
        /// Override to return damage multiplier based on current conditions.
        /// </summary>
        protected virtual float GetDamageMultiplier() => 1f;

        /// <summary>
        /// Override to return cooldown multiplier based on current conditions.
        /// </summary>
        protected virtual float GetCooldownMultiplier() => 1f;

        /// <summary>
        /// Override to return range multiplier based on current conditions.
        /// </summary>
        protected virtual float GetRangeMultiplier() => 1f;

        /// <summary>
        /// Apply damage multiplier to base damage.
        /// </summary>
        protected float ApplyDamageMultiplier(float baseDamage) => baseDamage * GetDamageMultiplier();

        /// <summary>
        /// Apply cooldown multiplier to base cooldown.
        /// </summary>
        protected float ApplyCooldownMultiplier(float baseCooldown) => baseCooldown * GetCooldownMultiplier();

        /// <summary>
        /// Apply range multiplier to base range.
        /// </summary>
        protected float ApplyRangeMultiplier(float baseRange) => baseRange * GetRangeMultiplier();

        // ========================================
        // CONDITIONAL ABILITY SYSTEM
        // ========================================

        /// <summary>
        /// Override to return additional conditions for ability activation.
        /// </summary>
        protected virtual bool CanActivateAbility(object stage, EntityPlayer target) => true;

        /// <summary>
        /// Override to return environmental conditions for ability activation.
        /// </summary>
        protected virtual bool CheckEnvironmentalConditions() => true;

        /// <summary>
        /// Override to return time-based conditions for ability activation.
        /// </summary>
        protected virtual bool CheckTimeConditions() => true;

        /// <summary>
        /// Enhanced ability check with all conditions.
        /// </summary>
        protected virtual bool CanActivateWithConditions(object stage, EntityPlayer target)
        {
            if (!CanActivateAbility(stage, target)) return false;
            if (!CheckEnvironmentalConditions()) return false;
            if (!CheckTimeConditions()) return false;
            return true;
        }

        // ========================================
        // UTILITY METHODS
        // ========================================

        /// <summary>
        /// Check if enough time has passed since last interval run using WatchedAttributes.
        /// </summary>
        protected bool ShouldRunInterval(string cooldownKey, int intervalMs, long nowMs)
        {
            if (entity?.WatchedAttributes == null) return true;

            long lastRun = entity.WatchedAttributes.GetLong(cooldownKey + ":lastRun", 0);
            if (nowMs - lastRun >= intervalMs)
            {
                entity.WatchedAttributes.SetLong(cooldownKey + ":lastRun", nowMs);
                entity.WatchedAttributes.MarkPathDirty(cooldownKey + ":lastRun");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Try to get current health fraction (0.0 to 1.0).
        /// </summary>
        protected bool TryGetHealthFraction(out float fraction)
        {
            fraction = 0f;
            if (entity == null) return false;

            var health = entity.GetBehavior<Vintagestory.GameContent.EntityBehaviorHealth>();
            if (health == null || health.MaxHealth <= 0) return false;

            fraction = health.Health / health.MaxHealth;
            return true;
        }

    }
}
