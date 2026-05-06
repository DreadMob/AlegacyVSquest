using ProtoBuf;

namespace VsQuest
{
    [ProtoContract]
    public class ShowQuestDialogMessage
    {
        [ProtoMember(10)]
        public string TitleLangKey { get; set; }

        [ProtoMember(11)]
        public string TextLangKey { get; set; }

        [ProtoMember(12)]
        public string Option1LangKey { get; set; }

        [ProtoMember(13)]
        public string Option2LangKey { get; set; }
    }
}
