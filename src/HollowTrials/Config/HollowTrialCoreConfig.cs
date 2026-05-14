namespace VsQuest
{
    /// <summary>
    /// Global configuration for the Hollow Trials system.
    /// Stored in the main mod config (alegacy-vsquest-config.json).
    /// </summary>
    public class HollowTrialCoreConfig
    {
        /// <summary>
        /// Enable debug logging for the trial system.
        /// </summary>
        public bool Debug { get; set; } = false;

        /// <summary>
        /// Number of in-game days between boss rotations.
        /// </summary>
        public double RotationDays { get; set; } = 60;

        /// <summary>
        /// Number of active trial bosses at any time (1 per tier).
        /// </summary>
        public int ActiveTrialCount { get; set; } = 3;

        /// <summary>
        /// Default hours without damage before soft reset.
        /// </summary>
        public double SoftResetIdleHours { get; set; } = 2.0;

        /// <summary>
        /// Default activation range in blocks.
        /// </summary>
        public float DefaultActivationRange { get; set; } = 120f;

        /// <summary>
        /// Default respawn hours after boss death.
        /// </summary>
        public double DefaultRespawnHours { get; set; } = 168;
    }
}
