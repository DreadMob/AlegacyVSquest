using Vintagestory.API.Server;

namespace VsQuest
{
    public class ShowQuizAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null || byPlayer == null) return;
            if (args == null || args.Length < 1 || string.IsNullOrWhiteSpace(args[0])) return;

            string quizId = args[0];
            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            questSystem?.QuizSystem?.StartQuiz(sapi, byPlayer, quizId, reset: true);
        }
    }
}
