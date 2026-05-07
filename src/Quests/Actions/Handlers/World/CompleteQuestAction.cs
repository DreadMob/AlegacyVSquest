using Vintagestory.API.Server;

namespace VsQuest
{
    public class CompleteQuestAction : EntityActionBase
    {
        protected override int MinArgs => 0;
        protected override string ActionName => "completequest";

        protected override void ExecuteAction(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();

            string questId = null;
            long questGiverId = message.questGiverId;

            if (args.Length == 0)
            {
                questId = message.questId;
            }
            else if (args.Length == 1)
            {
                questId = args[0];
            }
            else // args.Length >= 2
            {
                bool a0IsLong = long.TryParse(args[0], out long a0Long);
                bool a1IsLong = long.TryParse(args[1], out long a1Long);

                if (a0IsLong && !a1IsLong)
                {
                    questGiverId = a0Long;
                    questId = args[1];
                }
                else if (!a0IsLong && a1IsLong)
                {
                    questId = args[0];
                    questGiverId = a1Long;
                }
                else
                {
                    // Ambiguous, assume standard order: questId, questGiverId
                    questId = args[0];
                    if (!long.TryParse(args[1], out questGiverId))
                    {
                        throw new QuestException($"Could not parse questGiverId '{args[1]}' for completequest action in quest '{message?.questId}'.");
                    }
                }
            }

            if (string.IsNullOrEmpty(questId)) return;

            var questCompletedMessage = new QuestCompletedMessage() { questGiverId = questGiverId, questId = questId };
            questSystem.QuestPacketHandler.OnQuestCompleted(byPlayer, questCompletedMessage, sapi);
        }
    }
}
