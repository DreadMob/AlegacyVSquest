using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class PlayerCommandAction : PlayerActionBase
    {
        protected override int MinArgs => 1;
        protected override string ActionName => "playercommand";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            // Stub implementation
        }
    }
}
