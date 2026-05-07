namespace VsQuest
{
    public class PerformanceCoreConfig
    {
        // Global enable/disable
        public bool EnablePerformanceOptimizations { get; set; } = true;

        // Individual system toggles
        public bool EnableZeroPollEffects { get; set; } = true;
        public bool EnableInventoryFingerprinting { get; set; } = true;
        public bool EnableStatCoalescing { get; set; } = true;

        // ZeroPollEffectSystem settings
        public int EffectCleanupIntervalSeconds { get; set; } = 30;

        // InventoryFingerprintSystem settings
        public int FingerprintCheckIntervalMs { get; set; } = 250;
        public bool SkipFingerprintOnRapidCalls { get; set; } = true;

        // StatCoalescingEngine settings
        public int StatCoalesceWindowMs { get; set; } = 200;
        public int StatMaxDelayMs { get; set; } = 1000;

        /// <summary>
        /// Validates and applies defaults if needed.
        /// </summary>
        public void Validate()
        {
            if (EffectCleanupIntervalSeconds < 5) EffectCleanupIntervalSeconds = 5;
            if (EffectCleanupIntervalSeconds > 300) EffectCleanupIntervalSeconds = 300;

            if (FingerprintCheckIntervalMs < 50) FingerprintCheckIntervalMs = 50;
            if (FingerprintCheckIntervalMs > 2000) FingerprintCheckIntervalMs = 2000;

            if (StatCoalesceWindowMs < 50) StatCoalesceWindowMs = 50;
            if (StatCoalesceWindowMs > 1000) StatCoalesceWindowMs = 1000;

            if (StatMaxDelayMs < 200) StatMaxDelayMs = 200;
            if (StatMaxDelayMs > 5000) StatMaxDelayMs = 5000;
        }
    }
}
