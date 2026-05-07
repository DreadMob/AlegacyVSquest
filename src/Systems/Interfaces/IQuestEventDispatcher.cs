using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public interface IQuestEventDispatcher
    {
        void OnEntityKilled(ActiveQuest activeQuest, string entityCode, IPlayer byPlayer, IQuestContext context);
        void OnBlockPlaced(ActiveQuest activeQuest, string blockCode, int[] position, IPlayer byPlayer, IQuestContext context);
        void OnBlockBroken(ActiveQuest activeQuest, string blockCode, int[] position, IPlayer byPlayer, IQuestContext context);
        void OnBlockUsed(ActiveQuest activeQuest, string blockCode, int[] position, IPlayer byPlayer, IQuestContext context);
    }
}
