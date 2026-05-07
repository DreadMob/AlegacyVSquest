using ProtoBuf;

namespace VsQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ClaimQuestCompletionRewardMessage
    {
        public string rewardId { get; set; }
        public long questGiverId { get; set; }
    }
}
