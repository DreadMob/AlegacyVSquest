using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Quest/dialogue action: opens the Trial Warden shop GUI for the player.
    /// Usage in dialogue trigger: "openshop"
    /// </summary>
    public class OpenTrialShopAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null || byPlayer == null) return;

            var trialSystem = sapi.ModLoader.GetModSystem<HollowTrialSystem>();
            if (trialSystem == null) return;

            var handler = trialSystem.GetShopHandler();
            handler?.SendShopToPlayer(byPlayer);
        }
    }
}
