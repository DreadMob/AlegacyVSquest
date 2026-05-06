using ProtoBuf;
using System.Collections.Generic;

namespace VsQuest
{
    [ProtoContract]
    public class BossMusicUrlMapMessage
    {
        [ProtoMember(1)]
        public Dictionary<string, string> Urls { get; set; }
    }
}
