using System.Collections.Generic;

namespace VsQuest
{
    public class QuestCompletionRewardConfig
    {
        public List<QuestCompletionReward> rewards { get; set; } = new List<QuestCompletionReward>();
    }

    public class QuestCompletionReward
    {
        public string id { get; set; }
        public string scope { get; set; }
        public string targetId { get; set; }
        public string titleLangKey { get; set; }
        public string requirementLangKey { get; set; }
        public float x { get; set; }
        public float y { get; set; }
        public string iconItemCode { get; set; }
        public List<string> requiredQuestIds { get; set; } = new List<string>();
        public string rewardAction { get; set; }
        public string onceKey { get; set; }
    }
}
