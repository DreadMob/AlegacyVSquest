using Vintagestory.API.Server;

namespace VsQuest
{
    public interface IQuestLifecycleManager
    {
        void OnQuestAccepted(IServerPlayer fromPlayer, QuestAcceptedMessage message, ICoreServerAPI sapi, System.Func<string, System.Collections.Generic.List<ActiveQuest>> getPlayerQuests);
        void OnQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi, System.Func<string, System.Collections.Generic.List<ActiveQuest>> getPlayerQuests);
        bool ForceCompleteQuest(IServerPlayer player, QuestCompletedMessage message, ICoreServerAPI sapi, System.Func<string, System.Collections.Generic.List<ActiveQuest>> getPlayerQuests);
    }
}
