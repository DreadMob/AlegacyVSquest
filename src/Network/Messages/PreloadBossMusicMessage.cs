using ProtoBuf;

namespace VsQuest
{
    [ProtoContract]
    public class PreloadBossMusicMessage
    {
        [ProtoMember(1)]
        public string Url { get; set; }
    }
}
