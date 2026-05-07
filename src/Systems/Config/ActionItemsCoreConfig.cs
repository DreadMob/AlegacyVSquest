namespace VsQuest
{
    public class ActionItemsCoreConfig
    {
        public int HotbarEnforcerMaxSlotsPerTick { get; set; } = 64;
        public string BossHuntTrackerActionItemId { get; set; } = "albase:bosshunt-tracker";
        public float BossHuntTrackerCastDurationSec { get; set; } = 3f;
        public float BossHuntTrackerCastSlowdown { get; set; } = -0.5f;
        public string BossHuntTrackerCastSpeedStatKey { get; set; } = "alegacyvsquest:actionitemcast";
        public int InventoryScanIntervalMs { get; set; } = 1000;
        public int HotbarEnforceIntervalMs { get; set; } = 500;
    }
}
