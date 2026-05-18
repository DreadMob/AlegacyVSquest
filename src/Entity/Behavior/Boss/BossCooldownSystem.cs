using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// System for managing boss ability cooldowns.
    /// Provides centralized cooldown tracking and management.
    /// </summary>
    public class BossCooldownSystem
    {
        private readonly ICoreServerAPI sapi;
        private readonly Entity bossEntity;

        public BossCooldownSystem(ICoreServerAPI sapi, Entity bossEntity)
        {
            this.sapi = sapi;
            this.bossEntity = bossEntity;
        }

        /// <summary>
        /// Check if cooldown is ready for specific ability.
        /// </summary>
        public bool IsCooldownReady(string cooldownKey, float cooldownSeconds)
        {
            if (sapi == null || bossEntity == null) return false;

            long lastStartMs = bossEntity.WatchedAttributes.GetLong(cooldownKey, 0);
            long now = sapi.World.ElapsedMilliseconds;
            long cooldownMs = (long)(cooldownSeconds * 1000);

            // Never used before — always ready
            if (lastStartMs == 0) return true;

            // Server restart detection: ElapsedMilliseconds resets to 0 on restart,
            // but WatchedAttributes persist. If lastStartMs is in the future,
            // the cooldown data is stale and must be reset.
            if (lastStartMs > now && now >= 0)
            {
                bossEntity.WatchedAttributes.SetLong(cooldownKey, 0);
                bossEntity.WatchedAttributes.MarkPathDirty(cooldownKey);
                return true;
            }

            return now - lastStartMs >= cooldownMs;
        }

        /// <summary>
        /// Mark cooldown start time.
        /// </summary>
        public void MarkCooldownStart(string cooldownKey)
        {
            if (sapi == null || bossEntity == null) return;

            long now = sapi.World.ElapsedMilliseconds;
            bossEntity.WatchedAttributes.SetLong(cooldownKey, now);
            bossEntity.WatchedAttributes.MarkPathDirty(cooldownKey);
        }

        /// <summary>
        /// Get remaining cooldown time in milliseconds.
        /// </summary>
        public long GetRemainingCooldownMs(string cooldownKey, float cooldownSeconds)
        {
            if (sapi == null || bossEntity == null) return 0;

            long lastStartMs = bossEntity.WatchedAttributes.GetLong(cooldownKey, 0);
            long now = sapi.World.ElapsedMilliseconds;
            long cooldownMs = (long)(cooldownSeconds * 1000);
            long elapsed = now - lastStartMs;

            return Math.Max(0, cooldownMs - elapsed);
        }

        /// <summary>
        /// Get remaining cooldown time as fraction (0.0 to 1.0).
        /// </summary>
        public float GetCooldownProgress(string cooldownKey, float cooldownSeconds)
        {
            if (cooldownSeconds <= 0) return 1f;

            long remainingMs = GetRemainingCooldownMs(cooldownKey, cooldownSeconds);
            long totalMs = (long)(cooldownSeconds * 1000);

            return 1f - (float)remainingMs / totalMs;
        }

        /// <summary>
        /// Reset cooldown for ability.
        /// </summary>
        public void ResetCooldown(string cooldownKey)
        {
            if (sapi == null || bossEntity == null) return;

            bossEntity.WatchedAttributes.SetLong(cooldownKey, 0);
            bossEntity.WatchedAttributes.MarkPathDirty(cooldownKey);
        }

        /// <summary>
        /// Clear all cooldowns for the boss.
        /// </summary>
        public void ClearAllCooldowns()
        {
            if (sapi == null || bossEntity == null) return;

            var keysToRemove = new System.Collections.Generic.List<string>();
            foreach (var key in bossEntity.WatchedAttributes.Keys)
            {
                if (key.Contains("alegacyvsquest") && key.Contains("lastStartMs"))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                bossEntity.WatchedAttributes.RemoveAttribute(key);
            }

            if (keysToRemove.Count > 0)
            {
                bossEntity.WatchedAttributes.MarkPathDirty("alegacyvsquest");
            }
        }
    }
}
