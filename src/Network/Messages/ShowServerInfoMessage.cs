using ProtoBuf;

namespace VsQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ShowServerInfoMessage
    {
        public int startTab { get; set; }
    }
}
