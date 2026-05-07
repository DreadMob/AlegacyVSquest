using System;
using Vintagestory.API.Common.Entities;

namespace VsQuest
{
    /// <summary>
    /// Interface for tracking boss entities. Enables dependency injection and testing.
    /// </summary>
    public interface IBossEntityTracker
    {
        /// <summary>
        /// Get the currently tracked entity for a boss key.
        /// Returns null if not found or not alive.
        /// </summary>
        Entity GetTrackedEntity(string bossKey);

        /// <summary>
        /// Get the tracked entity regardless of alive status.
        /// </summary>
        Entity GetTrackedEntityAny(string bossKey);

        /// <summary>
        /// Event fired when a duplicate living entity is detected for a tracked bossKey.
        /// </summary>
        event Action<string> OnDuplicateBossDetected;

        /// <summary>
        /// Register a boss key to track.
        /// </summary>
        void RegisterBossKey(string bossKey);

        /// <summary>
        /// Unregister a boss key.
        /// </summary>
        void UnregisterBossKey(string bossKey);

        /// <summary>
        /// Get all tracked boss keys.
        /// </summary>
        string[] GetTrackedBossKeys();

        /// <summary>
        /// Force re-evaluation by scanning entities.
        /// </summary>
        void ForceScan();
    }
}
