using Vintagestory.API.Server;

namespace VsQuest
{
    public class AllowCharSelOnceAction : PlayerActionBase
    {
        protected override int MinArgs => 0;
        protected override string ActionName => "allowcharselonce";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            player.Entity.WatchedAttributes.SetBool("allowcharselonce", true);
            player.Entity.WatchedAttributes.MarkPathDirty("allowcharselonce");
        }
    }
}
