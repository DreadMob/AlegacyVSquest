using ProtoBuf;

namespace VsQuest
{
    [ProtoContract]
    public class ExecutePlayerCommandMessage
    {
        [ProtoMember(1)]
        public string Command { get; set; }
    }
}
