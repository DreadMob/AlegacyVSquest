using ProtoBuf;

namespace VsQuest
{
    [ProtoContract]
    public class DialogTriggerMessage
    {
        [ProtoMember(1)]
        public long EntityId { get; set; }

        [ProtoMember(2)]
        public string Trigger { get; set; }
    }
}
