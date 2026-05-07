using System;

namespace VsQuest
{
    public class QuestException : Exception
    {
        public QuestException() { }

        public QuestException(string message) : base(message) { }

        public QuestException(string message, Exception inner) : base(message, inner) { }
    }
}
