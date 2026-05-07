using ProtoBuf;

namespace VsQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ClaimReputationRewardsMessage
    {
        public long questGiverId { get; set; }
        public string scope { get; set; }
    }
}
