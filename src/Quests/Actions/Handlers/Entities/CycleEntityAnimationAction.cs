using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class CycleEntityAnimationAction : EntityActionBase
    {
        protected override int MinArgs => 0;
        protected override string ActionName => "cycleentityanimation";

        protected override void ExecuteAction(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            // Stub implementation
        }
    }
}
