using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public interface IQuestCompletionService
    {
        void CompleteQuest(
            IServerPlayer fromPlayer,
            QuestCompletedMessage message,
            ICoreServerAPI sapi,
            Entity questgiver,
            List<ActiveQuest> playerQuests,
            ActiveQuest activeQuest);
    }
}
