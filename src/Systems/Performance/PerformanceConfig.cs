using Vintagestory.API.Common;

namespace VsQuest.Systems.Performance
{
    /// <summary>
    /// Proxy for accessing performance settings from AlegacyVsQuestConfig.
    /// This provides a convenient static access point while the actual config lives in the main mod config.
    /// </summary>
    public static class PerformanceConfig
    {
        private static PerformanceCoreConfig _config;

        /// <summary>
        /// Initialize with the config from main mod config.
        /// Called by QuestSystem when loading configs.
        /// </summary>
        public static void Initialize(PerformanceCoreConfig config)
        {
            _config = config;
            _config?.Validate();
        }

        private static PerformanceCoreConfig C => _config ?? new PerformanceCoreConfig();

        // Global enable/disable
        public static bool EnablePerformanceOptimizations => C.EnablePerformanceOptimizations;

        // Individual system toggles
        public static bool EnableZeroPollEffects => C.EnableZeroPollEffects;
        public static bool EnableInventoryFingerprinting => C.EnableInventoryFingerprinting;
        public static bool EnableStatCoalescing => C.EnableStatCoalescing;

        // ZeroPollEffectSystem settings
        public static int EffectCleanupIntervalSeconds => C.EffectCleanupIntervalSeconds;

        // InventoryFingerprintSystem settings
        public static int FingerprintCheckIntervalMs => C.FingerprintCheckIntervalMs;
        public static bool SkipFingerprintOnRapidCalls => C.SkipFingerprintOnRapidCalls;

        // StatCoalescingEngine settings
        public static int StatCoalesceWindowMs => C.StatCoalesceWindowMs;
        public static int StatMaxDelayMs => C.StatMaxDelayMs;
    }
}
