using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public interface IQuestRewardService
    {
        void RewardPlayer(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi, Entity questgiver);
    }
}
