using System.Linq;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VsQuest
{
    public class RemoveTraitsAction : PlayerActionBase
    {
        protected override int MinArgs => 1;
        protected override string ActionName => "removetraits";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer byPlayer, string[] args)
        {
            var traits = byPlayer.Entity.WatchedAttributes
                .GetStringArray("extraTraits", new string[0])
                .ToHashSet();
            args.Foreach(trait => traits.Remove(trait));
            byPlayer.Entity.WatchedAttributes
                .SetStringArray("extraTraits", traits.ToArray());
        }
    }
}
