using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class CooldownBlockAction : EntityActionBase
    {
        protected override int MinArgs => 0;
        protected override string ActionName => "cooldownblock";

        protected override void ExecuteAction(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            // Stub implementation
        }
    }
}
