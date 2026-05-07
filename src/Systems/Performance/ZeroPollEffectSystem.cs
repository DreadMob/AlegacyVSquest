using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest.Systems.Performance
{
    /// <summary>
    /// Zero-poll effect system using RegisterCallback instead of tick-based polling.
    /// Eliminates all CPU overhead when no effects are active.
    /// </summary>
    public class ZeroPollEffectSystem : ModSystem
    {
        private ICoreServerAPI sapi;

        // Active callback tracking per player: entityId -> (effectType -> callbackId)
        private static readonly Dictionary<long, Dictionary<string, long>> ActiveCallbacks = new();

        // Pending cleanup for disconnected players
        private static readonly HashSet<long> PendingCleanup = new();

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            if (forSide != EnumAppSide.Server) return false;
            return PerformanceConfig.EnablePerformanceOptimizations 
                && PerformanceConfig.EnableZeroPollEffects;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            if (!PerformanceConfig.EnableZeroPollEffects)
            {
                api.Logger.Notification("[vsquest] ZeroPollEffectSystem is disabled in config");
                return;
            }

            api.Event.PlayerDisconnect += OnPlayerDisconnect;

            // Periodic cleanup of orphaned entries (safety net)
            var intervalMs = PerformanceConfig.EffectCleanupIntervalSeconds * 1000;
            api.Event.RegisterGameTickListener(OnPeriodicCleanup, intervalMs);

            api.Logger.Notification("[vsquest] ZeroPollEffectSystem started");
        }

        /// <summary>
        /// Applies a timed effect with zero polling overhead.
        /// Uses RegisterCallback for expiration - no CPU usage until effect expires.
        /// </summary>
        public static void ApplyTimedEffect(
            ICoreServerAPI api,
            EntityPlayer player,
            string effectType,
            int durationMs,
            Action<EntityPlayer> onApply,
            Action<EntityPlayer> onExpire)
        {
            if (!PerformanceConfig.EnableZeroPollEffects)
            {
                // Fallback: apply immediately without scheduling
                try { onApply?.Invoke(player); } catch (Exception ex) { api.Logger.Warning("[ZeroPollEffectSystem] Fallback apply failed: {0}", ex.Message); }
                return;
            }

            if (player?.EntityId == null) return;

            long entityId = player.EntityId;

            // Get or create callback dictionary for this player
            if (!ActiveCallbacks.TryGetValue(entityId, out var callbacks))
            {
                callbacks = new Dictionary<string, long>();
                ActiveCallbacks[entityId] = callbacks;
            }

            // Cancel existing callback for this effect type (overwrite)
            if (callbacks.TryGetValue(effectType, out var existingId))
            {
                api.Event.UnregisterCallback(existingId);
            }

            // Apply effect immediately
            try
            {
                onApply?.Invoke(player);
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[vsquest] ZeroPollEffectSystem: Error applying effect '{effectType}' for player {entityId}: {ex}");
            }

            // Schedule expiration callback - ZERO polling until then
            long callbackId = api.Event.RegisterCallback((dt) =>
            {
                try
                {
                    onExpire?.Invoke(player);
                }
                catch (Exception ex)
                {
                    api.Logger.Error($"[vsquest] ZeroPollEffectSystem: Error expiring effect '{effectType}' for player {entityId}: {ex}");
                }

                // Remove from tracking
                if (ActiveCallbacks.TryGetValue(entityId, out var cb))
                {
                    cb.Remove(effectType);

                    // Clean up if no more callbacks for this player
                    if (cb.Count == 0)
                    {
                        ActiveCallbacks.Remove(entityId);
                    }
                }
            }, durationMs);

            callbacks[effectType] = callbackId;
        }

        /// <summary>
        /// Cancels an active timed effect before it expires.
        /// </summary>
        public static void CancelEffect(ICoreServerAPI api, long entityId, string effectType)
        {
            if (!ActiveCallbacks.TryGetValue(entityId, out var callbacks)) return;
            if (!callbacks.TryGetValue(effectType, out var callbackId)) return;

            api.Event.UnregisterCallback(callbackId);
            callbacks.Remove(effectType);

            if (callbacks.Count == 0)
            {
                ActiveCallbacks.Remove(entityId);
            }
        }

        /// <summary>
        /// Checks if a player has an active effect of the given type.
        /// </summary>
        public static bool HasActiveEffect(long entityId, string effectType)
        {
            return ActiveCallbacks.TryGetValue(entityId, out var callbacks) && callbacks.ContainsKey(effectType);
        }

        /// <summary>
        /// Extends an existing effect's duration.
        /// </summary>
        public static void ExtendEffect(
            ICoreServerAPI api,
            EntityPlayer player,
            string effectType,
            int additionalDurationMs,
            Action<EntityPlayer> onApply,
            Action<EntityPlayer> onExpire)
        {
            // Cancel existing and re-apply with new duration
            CancelEffect(api, player.EntityId, effectType);
            ApplyTimedEffect(api, player, effectType, additionalDurationMs, onApply, onExpire);
        }

        /// <summary>
        /// Clears all effects for a player (e.g., on death or dimension change).
        /// </summary>
        public static void ClearAllEffects(ICoreServerAPI api, long entityId)
        {
            if (!ActiveCallbacks.TryGetValue(entityId, out var callbacks)) return;

            foreach (var callbackId in callbacks.Values)
            {
                api.Event.UnregisterCallback(callbackId);
            }

            ActiveCallbacks.Remove(entityId);
        }

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            // Mark for cleanup - actual cleanup happens in periodic task
            // to avoid modifying dictionary during iteration
            PendingCleanup.Add(player.Entity.EntityId);
        }

        private void OnPeriodicCleanup(float dt)
        {
            if (PendingCleanup.Count == 0) return;

            foreach (var entityId in PendingCleanup)
            {
                ClearAllEffects(sapi, entityId);
            }

            PendingCleanup.Clear();

            // Also clean up orphaned entries (players who logged out before this system was tracking)
            var toRemove = new List<long>();

            foreach (var kvp in ActiveCallbacks)
            {
                // If entity no longer exists in world, clean up
                if (sapi.World.GetEntityById(kvp.Key) == null)
                {
                    foreach (var callbackId in kvp.Value.Values)
                    {
                        sapi.Event.UnregisterCallback(callbackId);
                    }
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                ActiveCallbacks.Remove(id);
            }
        }

        /// <summary>
        /// Gets the number of active effects for monitoring/debugging.
        /// </summary>
        public static int GetActiveEffectCount()
        {
            int count = 0;
            foreach (var callbacks in ActiveCallbacks.Values)
            {
                count += callbacks.Count;
            }
            return count;
        }

        /// <summary>
        /// Gets the number of players with active effects for monitoring/debugging.
        /// </summary>
        public static int GetActivePlayerCount()
        {
            return ActiveCallbacks.Count;
        }
    }
}
