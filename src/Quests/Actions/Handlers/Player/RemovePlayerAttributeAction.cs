using Vintagestory.API.Server;

namespace VsQuest
{
    public class RemovePlayerAttributeAction : PlayerActionBase
    {
        protected override int MinArgs => 1;
        protected override string ActionName => "removeplayerattribute";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            player.Entity.WatchedAttributes.RemoveAttribute(args[0]);
        }
    }
}
