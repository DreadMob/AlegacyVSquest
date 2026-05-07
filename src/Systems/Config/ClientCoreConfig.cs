namespace VsQuest
{
    public class ClientCoreConfig
    {
        public BossMusicCoreConfig BossMusic { get; set; } = new BossMusicCoreConfig();
        public ViewDistanceFogCoreConfig ViewDistanceFog { get; set; } = new ViewDistanceFogCoreConfig();
    }

    public class BossMusicCoreConfig
    {
        public float VolumeMul { get; set; } = 0.3f;
        public float DefaultFadeOutSeconds { get; set; } = 2f;
    }

    public class ViewDistanceFogCoreConfig
    {
        public int TickIntervalMs { get; set; } = 100;
        public float BaseDensity { get; set; } = 0.00125f;
        public float FogMinMul { get; set; } = 0.03f;
        public float NegativeFogDensityAddMul { get; set; } = 0.006f;
        public float PositiveFogDensitySubMul { get; set; } = 0.0009f;
    }
}
