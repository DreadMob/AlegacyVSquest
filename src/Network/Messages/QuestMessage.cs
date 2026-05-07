using ProtoBuf;

namespace VsQuest
{
    [ProtoContract]
    public class QuestAcceptedMessage : QuestMessage
    {
    }

    [ProtoContract]
    public class QuestCompletedMessage : QuestMessage
    {
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [ProtoInclude(10, typeof(QuestAcceptedMessage))]
    [ProtoInclude(11, typeof(QuestCompletedMessage))]
    public abstract class QuestMessage
    {
        public string questId { get; set; }

        public long questGiverId { get; set; }
    }
}
