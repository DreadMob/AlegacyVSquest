using System.Collections.Generic;

namespace VsQuest
{
    /// <summary>
    /// Interface for providing boss hunt configurations. Enables dependency injection and testing.
    /// </summary>
    public interface IBossHuntConfigProvider
    {
        /// <summary>
        /// Get configuration for a specific boss key.
        /// </summary>
        BossHuntSystem.BossHuntConfig GetConfig(string bossKey);

        /// <summary>
        /// Get all valid configurations.
        /// </summary>
        IEnumerable<BossHuntSystem.BossHuntConfig> GetAllConfigs();

        /// <summary>
        /// Check if a boss key is allowed.
        /// </summary>
        bool IsBossKeyAllowed(string bossKey);

        /// <summary>
        /// Register a boss key as allowed.
        /// </summary>
        void RegisterBossHunt(string bossKey);
    }
}
