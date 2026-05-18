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
            // Support both 'maxTargetRange' and 'range' for backwards compatibility
            maxTargetRange = json["maxTargetRange"].AsFloat(json["range"].AsFloat(30f));
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

        private readonly List<long> activeCallbackIds = new List<long>();
        
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
        /// Default implementation returns false for boss clones.
        /// </summary>
        protected virtual bool ShouldCheckAbility() => !IsBossClone;

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
                Sapi.Logger.Debug("[BossAbility] {0} initialized for entity {1}", PropertyName(), entity?.Code ?? "null");
            }
            else
            {
                // This is normal for client-side behaviors
            }

            InitializeStages(attributes);
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (entity == null) return;

            // Lazy-init Sapi (may be null during Initialize if entity wasn't spawned yet)
            if (Sapi == null)
            {
                Sapi = entity.Api as ICoreServerAPI;
                if (Sapi == null) return;

                CooldownSystem = new BossCooldownSystem(Sapi, entity);
                TargetingSystem = new BossTargetingSystem(Sapi, entity);
                MarkingSystem = new BossMarkingSystem(Sapi, entity);
            }

            if (!entity.Alive)
            {
                // For periodic tick abilities (respawn, auras), still allow OnPeriodicTick
                // even when entity is dead, so they can handle corpse cleanup/timers
                if (UsePeriodicTick())
                {
                    long now = Sapi.World.ElapsedMilliseconds;
                    if (now - lastCheckMs < CheckIntervalMs) return;
                    lastCheckMs = now;
                    OnPeriodicTick(dt);
                }
                else
                {
                    StopAbility();
                }
                return;
            }

            // Periodic tick path for auras/rituals (always active, bypass activation cycle)
            if (UsePeriodicTick())
            {
                // Clones should not run periodic abilities
                if (IsBossClone) return;

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

            // Debug: log ability check
            if (Sapi.World.Rand.NextDouble() < 0.01) // 1% chance to log
            {
                Sapi.Logger.Debug("[BossAbility] {0} checking for entity {1}, stages={2}, healthBased={3}",
                    PropertyName(), entity.Code, GetStageCount(), UseHealthBasedStages());
            }

            CheckAbility();
        }

        protected virtual void CheckAbility()
        {
            // Universal check: never activate abilities on boss clones
            if (IsBossClone) return;

            // Debug mode: only allow the selected ability to activate
            if (IsDebugModeActive && !IsDebugSelectedAbility) return;

            if (!ShouldCheckAbility())
            {
                // Log why ShouldCheckAbility returned false
                if (Sapi?.World?.Rand?.NextDouble() < 0.005) // 0.5% chance to log
                {
                    Sapi.Logger.Debug("[BossAbility] {0} ShouldCheckAbility=false for {1}, IsAbilityActive={2}", PropertyName(), entity.Code, IsAbilityActive);
                }
                return;
            }
            if (!entity.Alive) return;

            object stage = null;
            int stageIndex = -1;

            if (UseHealthBasedStages())
            {
                if (!entity.TryGetHealthFraction(out float frac))
                {
                    return;
                }
                (stage, stageIndex) = FindStageForHealth(frac);
                if (stage == null)
                {
                    return;
                }
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

            if (!IsCooldownReady(stage))
            {
                return;
            }

            EntityPlayer target = null;
            if (RequiresTarget())
            {
                float maxRange = ApplyRangeMultiplier(GetMaxTargetRange(stage));
                if (!TargetingSystem.TryFindTarget(maxRange, MinTargetRange, out target, out float dist))
                {
                    return;
                }
            }

            if (!CanActivateWithConditions(stage, target))
            {
                return;
            }

            abilityActive = true;
            ActivateAbility(stage, stageIndex, target);
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            StopAbility();
            abilityActive = false;
            CleanupPlayerDebuffs();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopAbility();
            abilityActive = false;
            CleanupPlayerDebuffs();
            UnregisterAllTrackedCallbacks();
            base.OnEntityDespawn(despawn);
        }

        /// <summary>
        /// Remove all boss-applied debuffs from nearby players on death/despawn.
        /// Ensures players are never stuck with permanent debuffs.
        /// </summary>
        private void CleanupPlayerDebuffs()
        {
            if (Sapi == null) return;

            try
            {
                var players = Sapi.World.AllOnlinePlayers;
                if (players == null) return;

                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] is not IServerPlayer sp) continue;
                    var pe = sp.Entity;
                    if (pe == null) continue;

                    // Remove all boss-applied stat modifiers
                    pe.Stats?.Remove("walkspeed", "cursestun");
                    pe.Stats?.Remove("walkspeed", "curseslow");
                    pe.Stats?.Remove("walkspeed", "gravitystun");
                    pe.Stats?.Remove("walkspeed", "projchain");
                    pe.Stats?.Remove("rangedWeaponsAcc", "bossblind");

                    // Remove all boss-applied WatchedAttributes flags
                    if (pe.WatchedAttributes != null)
                    {
                        bool changed = false;
                        if (pe.WatchedAttributes.GetBool("alegacyvsquest:blinded", false))
                        {
                            pe.WatchedAttributes.SetBool("alegacyvsquest:blinded", false);
                            changed = true;
                        }
                        if (pe.WatchedAttributes.GetBool("alegacyvsquest:disarmed", false))
                        {
                            pe.WatchedAttributes.SetBool("alegacyvsquest:disarmed", false);
                            changed = true;
                        }
                        if (pe.WatchedAttributes.GetBool("alegacyvsquest:silenced", false))
                        {
                            pe.WatchedAttributes.SetBool("alegacyvsquest:silenced", false);
                            changed = true;
                        }
                        if (changed)
                        {
                            pe.WatchedAttributes.MarkPathDirty("alegacyvsquest:blinded");
                            pe.WatchedAttributes.MarkPathDirty("alegacyvsquest:disarmed");
                            pe.WatchedAttributes.MarkPathDirty("alegacyvsquest:silenced");
                        }
                    }
                }
            }
            catch { }
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
        /// Play sound with pitch randomization, optional delay, and volume boost.
        /// </summary>
        protected void TryPlaySound(string sound, float range = 24f, int startDelayMs = 0, float volume = 1f)
        {
            if (Sapi == null || string.IsNullOrWhiteSpace(sound)) return;

            AssetLocation soundLoc = AssetLocation.Create(sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            // Pitch randomization (0.75-1.25)
            float pitch = (float)Sapi.World.Rand.NextDouble() * 0.5f + 0.75f;

            // Apply vsquestSoundPitchMul from entity attributes
            try
            {
                float pitchMult = entity?.Properties?.Attributes?["vsquestSoundPitchMul"].AsFloat(1f) ?? 1f;
                if (pitchMult > 0f && Math.Abs(pitchMult - 1f) > 0.0001f)
                {
                    pitch *= pitchMult;
                }
            }
            catch { }

            float finalRange = range > 0f ? range : 64f;
            float finalVolume = volume * 1.5f; // Boost volume 1.5x

            if (startDelayMs > 0)
            {
                Sapi.Event.RegisterCallback(_ =>
                {
                    try
                    {
                        Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, finalRange, finalVolume);
                    }
                    catch (Exception ex)
                    {
                        entity?.Api?.Logger?.Error($"[vsquest] Exception in delayed TryPlaySound: {ex}");
                    }
                }, startDelayMs);
            }
            else
            {
                Sapi.World.PlaySoundAt(soundLoc, entity, null, pitch, finalRange, finalVolume);
            }
        }

        protected void TryPlayAnimation(string animation)
        {
            if (string.IsNullOrWhiteSpace(animation)) return;
            if (entity?.AnimManager == null) return;

            // Try to resolve animation metadata for proper playback with speed, blend mode, weight
            var animations = entity.Properties?.Client?.AnimationsByMetaCode;
            if (animations != null && animations.TryGetValue(animation, out var meta) && meta != null)
            {
                entity.AnimManager.StartAnimation(meta.Clone());
            }
            else
            {
                // Fallback to string-based animation
                entity.AnimManager.StartAnimation(animation);
            }
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
                if (stagesArray == null)
                {
                    Sapi?.Logger.Warning("[BossAbility] {0}: No 'stages' array found in attributes", PropertyName());
                    return result;
                }

                foreach (var stageObj in stagesArray)
                {
                    if (stageObj == null || !stageObj.Exists) continue;
                    var stage = new T();
                    stage.FromJson(stageObj);
                    result.Add(stage);
                }

                Sapi?.Logger.Notification("[BossAbility] {0}: Parsed {1} stages", PropertyName(), result.Count);
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
        /// Register a callback and track its ID so it can be cancelled on despawn.
        /// </summary>
        protected long RegisterCallbackTracked(Action<float> action, int ms)
        {
            if (Sapi == null) return 0;
            long id = Sapi.Event.RegisterCallback(action, ms);
            if (id != 0) activeCallbackIds.Add(id);
            return id;
        }

        /// <summary>
        /// Unregister all callbacks tracked via RegisterCallbackTracked.
        /// </summary>
        protected void UnregisterAllTrackedCallbacks()
        {
            if (Sapi == null) return;
            foreach (var id in activeCallbackIds)
            {
                if (id != 0) Sapi.Event.UnregisterCallback(id);
            }
            activeCallbackIds.Clear();
        }

        /// <summary>
        /// Force-activate this ability for debugging purposes.
        /// Bypasses cooldown, health threshold, and range checks.
        /// </summary>
        /// <param name="target">Target player (can be null for abilities that don't require a target).</param>
        /// <param name="stageIndex">Stage index to use (default 0).</param>
        /// <returns>True if ability was activated, false if preconditions failed.</returns>
        public bool ForceActivate(EntityPlayer target, int stageIndex = 0)
        {
            if (Sapi == null || entity == null || !entity.Alive) return false;

            // Reset cooldown so ability can fire immediately
            CooldownSystem?.ResetCooldown(CooldownKey);

            if (stageIndex < 0) stageIndex = 0;
            if (stageIndex >= GetStageCount()) stageIndex = GetStageCount() - 1;

            object stage = GetStageCount() > 0 ? GetStage(stageIndex) : null;

            if (stage == null && UseHealthBasedStages()) return false;

            if (RequiresTarget() && target == null)
            {
                // Try to find any target in extended range
                if (TargetingSystem != null && TargetingSystem.TryFindTarget(100f, 0f, out target, out _))
                {
                    // found
                }
                else
                {
                    return false;
                }
            }

            abilityActive = true;
            ActivateAbility(stage, stageIndex, target);
            return true;
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

        /// <summary>
        /// Check if this entity is a boss clone (should not activate abilities).
        /// </summary>
        protected bool IsBossClone => entity?.WatchedAttributes?.GetBool("alegacyvsquest:bossclone", false) ?? false;

        // ========================================
        // DEBUG MODE SUPPORT
        // ========================================

        /// <summary>
        /// Check if debug boss ability mode is active on this entity.
        /// </summary>
        private bool IsDebugModeActive =>
            entity?.WatchedAttributes?.GetBool("alegacyvsquest:debugba:active", false) ?? false;

        /// <summary>
        /// Check if this ability is the currently selected debug ability.
        /// </summary>
        private bool IsDebugSelectedAbility
        {
            get
            {
                string selected = entity?.WatchedAttributes?.GetString("alegacyvsquest:debugba:ability", null);
                return string.Equals(selected, PropertyName(), StringComparison.OrdinalIgnoreCase);
            }
        }

        // ========================================
        // ABILITY MODIFIER SYSTEM
        // ========================================

        /// <summary>
        /// Override to return damage multiplier based on current conditions.
        /// Base implementation reads trial tier ability damage multiplier from WatchedAttributes.
        /// </summary>
        protected virtual float GetDamageMultiplier()
        {
            float trialMult = entity?.WatchedAttributes?.GetFloat("alegacyvsquest:trial:abilityDamageMult", 1f) ?? 1f;
            float enrageMult = entity?.WatchedAttributes?.GetFloat("alegacyvsquest:enrage:damageMultiplier", 1f) ?? 1f;
            return trialMult * enrageMult;
        }

        /// <summary>
        /// Override to return cooldown multiplier based on current conditions.
        /// Base implementation reads trial tier ability cooldown multiplier from WatchedAttributes.
        /// </summary>
        protected virtual float GetCooldownMultiplier()
        {
            return entity?.WatchedAttributes?.GetFloat("alegacyvsquest:trial:abilityCooldownMult", 1f) ?? 1f;
        }

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
            if (health != null && health.MaxHealth > 0)
            {
                fraction = health.Health / health.MaxHealth;
                return true;
            }

            // Fallback: read directly from WatchedAttributes if behavior lookup fails
            var wa = entity.WatchedAttributes;
            if (wa != null)
            {
                var healthTree = wa.GetTreeAttribute("health");
                if (healthTree != null)
                {
                    float maxHealth = healthTree.GetFloat("maxhealth", 0f);
                    if (maxHealth <= 0f) maxHealth = healthTree.GetFloat("basemaxhealth", 0f);
                    float curHealth = healthTree.GetFloat("currenthealth", 0f);
                    if (maxHealth > 0f && curHealth > 0f)
                    {
                        fraction = curHealth / maxHealth;
                        return true;
                    }
                }
            }

            return false;
        }

    }
}
