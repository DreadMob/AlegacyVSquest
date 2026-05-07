using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Handles quiz packet messages: show, open, submit answer.
    /// </summary>
    public class QuizPacketHandler
    {
        private readonly QuizSystem quizSystem;

        public QuizPacketHandler(QuizSystem quizSystem)
        {
            this.quizSystem = quizSystem;
        }

        // Server-side handlers

        public void OnOpenQuizMessage(IServerPlayer player, OpenQuizMessage message, ICoreServerAPI sapi)
        {
            quizSystem?.OnOpenQuizMessage(player, message, sapi);
        }

        public void OnSubmitQuizAnswerMessage(IServerPlayer player, SubmitQuizAnswerMessage message, ICoreServerAPI sapi)
        {
            quizSystem?.OnSubmitQuizAnswerMessage(player, message, sapi);
        }

        // Client-side handlers

        public void OnShowQuizMessage(ShowQuizMessage message, ICoreClientAPI capi)
        {
            quizSystem?.OnShowQuizMessage(message, capi);
        }
    }
}
