using System;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class AcceptQuestAction : EntityActionBase
    {
        protected override int MinArgs => 1;
        protected override string ActionName => "acceptquest";

        private readonly Action<IServerPlayer, QuestAcceptedMessage, ICoreServerAPI> onQuestAcceptedCallback;
        private readonly ICoreServerAPI sapi;

        public AcceptQuestAction(ICoreServerAPI sapi, Action<IServerPlayer, QuestAcceptedMessage, ICoreServerAPI> onQuestAcceptedCallback)
        {
            this.sapi = sapi;
            this.onQuestAcceptedCallback = onQuestAcceptedCallback;
        }

        protected override void ExecuteAction(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            string questId = null;
            long questGiverId = message.questGiverId;

            if (args.Length == 1)
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
                        throw new QuestException($"Could not parse questGiverId '{args[1]}' for acceptquest action in quest '{message?.questId}'.");
                    }
                }
            }

            if (string.IsNullOrEmpty(questId)) return;

            onQuestAcceptedCallback(byPlayer, new QuestAcceptedMessage() { questGiverId = questGiverId, questId = questId }, sapi);
        }
    }
}
