using ProtoBuf;

namespace VsQuest
{
    [ProtoContract]
    public class SubmitQuizAnswerMessage
    {
        [ProtoMember(1)]
        public string QuizId { get; set; }

        [ProtoMember(2)]
        public int SelectedOption { get; set; }

        [ProtoMember(3)]
        public bool Retry { get; set; }
    }
}
