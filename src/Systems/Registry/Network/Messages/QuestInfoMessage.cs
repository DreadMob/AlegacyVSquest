using ProtoBuf;
using System.Collections.Generic;

namespace VsQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class QuestInfoMessage
    {
        public long questGiverId { get; set; }
        public List<string> availableQestIds { get; set; }
        public List<ActiveQuest> activeQuests { get; set; }
        public string noAvailableQuestDescLangKey { get; set; }
        public string noAvailableQuestCooldownDescLangKey { get; set; }
        public int noAvailableQuestCooldownDaysLeft { get; set; }
        public int noAvailableQuestRotationDaysLeft { get; set; }
        public string reputationNpcId { get; set; }
        public string reputationFactionId { get; set; }
        public int reputationNpcValue { get; set; }
        public int reputationFactionValue { get; set; }
        public string reputationNpcRankLangKey { get; set; }
        public string reputationFactionRankLangKey { get; set; }
        public string reputationNpcTitleLangKey { get; set; }
        public string reputationFactionTitleLangKey { get; set; }
        public bool reputationNpcHasRewards { get; set; }
        public bool reputationFactionHasRewards { get; set; }
        public int reputationNpcRewardsCount { get; set; }
        public int reputationFactionRewardsCount { get; set; }
        public List<QuestCompletionRewardStatus> completionRewards { get; set; }
        public List<ReputationRankRewardStatus> reputationNpcRankRewards { get; set; }
        public List<ReputationRankRewardStatus> reputationFactionRankRewards { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class QuestCompletionRewardStatus
    {
        public string id { get; set; }
        public string title { get; set; }
        public string requirementText { get; set; }
        public float x { get; set; }
        public float y { get; set; }
        public string status { get; set; }
        public string iconItemCode { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ReputationRankRewardStatus
    {
        public int min { get; set; }
        public string rankLangKey { get; set; }
        public string status { get; set; }
        public string iconItemCode { get; set; }
    }
}
