namespace VsQuest
{
    public class BossHuntCoreConfig
    {
        public bool Debug { get; set; } = false;
        public double SoftResetIdleHours { get; set; } = 1.0;
        public double SoftResetAntiSpamHours { get; set; } = 0.25;
        public double RelocatePostponeHours { get; set; } = 0.25;
        public double BossEntityScanIntervalHours { get; set; } = 1.0 / 60.0;
        public double DebugLogThrottleHours { get; set; } = 0.02;
        public float DefaultActivationRange { get; set; } = 200f;
    }
}
