using ProtoBuf;

namespace VsQuest
{
    [ProtoContract]
    public class ShowNotificationMessage
    {
        [ProtoMember(1)]
        public string Notification { get; set; }

        [ProtoMember(2)]
        public string Template { get; set; }

        [ProtoMember(3)]
        public int Need { get; set; }

        [ProtoMember(4)]
        public string MobCode { get; set; }
    }
}
