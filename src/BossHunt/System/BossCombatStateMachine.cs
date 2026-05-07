using System;

namespace VsQuest
{
    /// <summary>
    /// Explicit combat states for boss entities.
    /// Replaces scattered boolean flags with a clear state machine.
    /// </summary>
    public enum BossCombatState
    {
        /// <summary>
        /// Boss not spawned, waiting for activation.
        /// </summary>
        Dormant,

        /// <summary>
        /// Boss is currently spawning (transition state).
        /// </summary>
        Spawning,

        /// <summary>
        /// Boss is alive and actively engaged in combat.
        /// </summary>
        InCombat,

        /// <summary>
        /// Boss is alive but has not received damage recently.
        /// May trigger soft reset/despawn.
        /// </summary>
        OutOfCombat,

        /// <summary>
        /// Boss is transitioning between phases (multi-phase rebirth).
        /// </summary>
        Rebirthing,

        /// <summary>
        /// Boss is dead, corpse present in world.
        /// </summary>
        Corpse
    }

    /// <summary>
    /// State machine for boss combat lifecycle.
    /// Manages transitions between Dormant, Spawning, InCombat, OutOfCombat, Rebirthing, and Corpse states.
    /// </summary>
    public class BossCombatStateMachine
    {
        private BossCombatState currentState = BossCombatState.Dormant;
        private double lastDamageTimeHours;
        private double spawnTimeHours;
        private double outOfCombatThresholdHours = 1.0;

        public BossCombatState CurrentState => currentState;

        public double LastDamageTimeHours => lastDamageTimeHours;
        public double SpawnTimeHours => spawnTimeHours;

        /// <summary>
        /// Configure the out-of-combat threshold.
        /// Default: 1.0 in-game hour.
        /// </summary>
        public void SetOutOfCombatThreshold(double hours)
        {
            outOfCombatThresholdHours = hours > 0 ? hours : 1.0;
        }

        /// <summary>
        /// Called when boss spawns into the world.
        /// </summary>
        public void OnSpawn(double nowHours)
        {
            spawnTimeHours = nowHours;
            lastDamageTimeHours = 0;
            TransitionTo(BossCombatState.Spawning);
            TransitionTo(BossCombatState.InCombat);
        }

        /// <summary>
        /// Called when boss receives damage.
        /// </summary>
        public void OnDamageReceived(double nowHours)
        {
            lastDamageTimeHours = nowHours;

            if (currentState == BossCombatState.OutOfCombat)
            {
                TransitionTo(BossCombatState.InCombat);
            }
        }

        /// <summary>
        /// Called periodically to check for combat timeout.
        /// Returns true if state changed from InCombat to OutOfCombat.
        /// </summary>
        public bool OnCombatTimeout(double nowHours)
        {
            if (currentState != BossCombatState.InCombat) return false;

            double idleReferenceHours = lastDamageTimeHours > 0
                ? lastDamageTimeHours
                : spawnTimeHours;

            double idleDuration = nowHours - idleReferenceHours;

            if (idleDuration >= outOfCombatThresholdHours)
            {
                TransitionTo(BossCombatState.OutOfCombat);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Called when boss dies.
        /// </summary>
        public void OnDeath()
        {
            TransitionTo(BossCombatState.Corpse);
        }

        /// <summary>
        /// Called when boss enters rebirth phase transition.
        /// </summary>
        public void OnRebirthStart()
        {
            TransitionTo(BossCombatState.Rebirthing);
        }

        /// <summary>
        /// Called when boss completes rebirth (new phase spawned).
        /// </summary>
        public void OnRebirthComplete(double nowHours)
        {
            spawnTimeHours = nowHours;
            lastDamageTimeHours = 0;
            TransitionTo(BossCombatState.InCombat);
        }

        /// <summary>
        /// Called when boss is despawned (soft reset, relocation, or rotation).
        /// </summary>
        public void OnDespawn()
        {
            TransitionTo(BossCombatState.Dormant);
            ResetTimers();
        }

        /// <summary>
        /// Reset internal timers (used on despawn).
        /// </summary>
        private void ResetTimers()
        {
            lastDamageTimeHours = 0;
            spawnTimeHours = 0;
        }

        /// <summary>
        /// Internal state transition with validation.
        /// </summary>
        private void TransitionTo(BossCombatState newState)
        {
            if (!IsValidTransition(currentState, newState))
            {
                return;
            }

            currentState = newState;
        }

        /// <summary>
        /// Validate that a state transition is allowed.
        /// </summary>
        private bool IsValidTransition(BossCombatState from, BossCombatState to)
        {
            return (from, to) switch
            {
                (BossCombatState.Dormant, BossCombatState.Spawning) => true,
                (BossCombatState.Spawning, BossCombatState.InCombat) => true,
                (BossCombatState.InCombat, BossCombatState.OutOfCombat) => true,
                (BossCombatState.InCombat, BossCombatState.Rebirthing) => true,
                (BossCombatState.InCombat, BossCombatState.Corpse) => true,
                (BossCombatState.OutOfCombat, BossCombatState.InCombat) => true,
                (BossCombatState.OutOfCombat, BossCombatState.Corpse) => true,
                (BossCombatState.Rebirthing, BossCombatState.InCombat) => true,
                (BossCombatState.Rebirthing, BossCombatState.Corpse) => true,
                (BossCombatState.Corpse, BossCombatState.Dormant) => true,
                (BossCombatState.Corpse, BossCombatState.Rebirthing) => true,
                _ => false
            };
        }

        /// <summary>
        /// Check if boss is considered "active" (not Dormant or Corpse).
        /// </summary>
        public bool IsActive => currentState == BossCombatState.InCombat
            || currentState == BossCombatState.OutOfCombat
            || currentState == BossCombatState.Rebirthing;

        /// <summary>
        /// Check if boss is alive (not Corpse or Dormant).
        /// </summary>
        public bool IsAlive => currentState != BossCombatState.Corpse
            && currentState != BossCombatState.Dormant;
    }
}
