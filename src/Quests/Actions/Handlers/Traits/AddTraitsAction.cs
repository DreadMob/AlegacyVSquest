using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class AddTraitsAction : PlayerActionBase
    {
        protected override int MinArgs => 0;
        protected override string ActionName => "addtraits";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            // Stub implementation
        }
    }
}
