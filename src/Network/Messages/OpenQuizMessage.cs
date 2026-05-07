using ProtoBuf;

namespace VsQuest
{
    [ProtoContract]
    public class OpenQuizMessage
    {
        [ProtoMember(1)]
        public string QuizId { get; set; }

        [ProtoMember(2)]
        public bool Reset { get; set; }
    }
}
