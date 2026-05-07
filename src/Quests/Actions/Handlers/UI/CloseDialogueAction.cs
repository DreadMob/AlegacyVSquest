using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class CloseDialogueAction : PlayerActionBase
    {
        protected override int MinArgs => 0;
        protected override string ActionName => "closedialogue";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            // Stub implementation
        }
    }
}
