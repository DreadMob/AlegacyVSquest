using ProtoBuf;

namespace VsQuest
{
    [ProtoContract]
    public class ShowQuizMessage
    {
        [ProtoMember(1)]
        public string QuizId { get; set; }

        [ProtoMember(2)]
        public int QuestionIndex { get; set; }

        [ProtoMember(3)]
        public int TotalQuestions { get; set; }

        [ProtoMember(4)]
        public int Correct { get; set; }

        [ProtoMember(5)]
        public int Wrong { get; set; }

        [ProtoMember(6)]
        public int NeededCorrect { get; set; }

        [ProtoMember(7)]
        public bool IsFinished { get; set; }

        [ProtoMember(8)]
        public string TitleLangKey { get; set; }

        [ProtoMember(9)]
        public string QuestionLangKey { get; set; }

        [ProtoMember(10)]
        public string OptionALangKey { get; set; }

        [ProtoMember(11)]
        public string OptionBLangKey { get; set; }

        [ProtoMember(12)]
        public string OptionCLangKey { get; set; }

        [ProtoMember(13)]
        public string OptionDLangKey { get; set; }

        [ProtoMember(14)]
        public string BodyLangKey { get; set; }

        [ProtoMember(15)]
        public string ProgressTemplateLangKey { get; set; }

        [ProtoMember(16)]
        public string ResultTemplateLangKey { get; set; }

        [ProtoMember(17)]
        public string RetryButtonLangKey { get; set; }

        [ProtoMember(18)]
        public string CloseButtonLangKey { get; set; }
    }
}
