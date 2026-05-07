using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public interface IQuestNotificationService
    {
        void BroadcastQuestCompleted(IServerPlayer player, string questId);
        void BroadcastQuestStageCompleted(IServerPlayer player, string questId, int stage);
        bool ShouldNotifyOnComplete(Quest quest);
    }
}
