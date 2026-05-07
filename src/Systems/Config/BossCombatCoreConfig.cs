namespace VsQuest
{
    public class BossCombatCoreConfig
    {
        public double BossKillCreditMinShareCeil { get; set; } = 0.3;
        public double BossKillCreditMinShareFloor { get; set; } = 0.05;
        public float BossKillHealFraction { get; set; } = 0.17f;
        public bool BossPassiveRegenEnabled { get; set; } = true;
    }
}
