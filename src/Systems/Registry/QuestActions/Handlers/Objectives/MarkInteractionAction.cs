using Vintagestory.API.Server;

namespace VsQuest
{
    public class MarkInteractionAction : PlayerActionBase
    {
        protected override int MinArgs => 1;
        protected override string ActionName => "markinteraction";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer byPlayer, string[] args)
        {
            var wa = byPlayer.Entity.WatchedAttributes;

            // New storage: one bool per coordinate string
            var key = $"alegacyvsquest:interactat:{args[0]}";
            wa.SetBool(key, true);
            wa.MarkPathDirty(key);
        }
    }
}
