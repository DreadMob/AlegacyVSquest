namespace VsQuest
{
    public class QuestTickCoreConfig
    {
        public double MissingQuestLogThrottleHours { get; set; } = 1.0 / 60.0;
        public double PassiveCompletionThrottleHours { get; set; } = 1.0 / 3600.0;
    }
}
