using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class LandClaimMaxAreasAction : PlayerActionBase
    {
        protected override int MinArgs => 0;
        protected override string ActionName => "landclaimmaxareas";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            var key = "landclaimmaxareas";

            int value;
            if (args == null || args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                value = player.Entity.WatchedAttributes.GetInt(key, 0);

                // If the extra max areas was set by an admin command (ServerData only), do not let this quest action lower it.
                if (player.ServerData != null)
                {
                    value = System.Math.Max(value, player.ServerData.ExtraLandClaimAreas);
                }
            }
            else if (!int.TryParse(args[0], out value))
            {
                sapi.Logger.Error($"[vsquest] 'landclaimmaxareas' action argument 'value' must be an int, but got '{args[0]}'.");
                return;
            }

            if (player.ServerData != null)
            {
                player.ServerData.ExtraLandClaimAreas = value;
            }

            player.Entity.WatchedAttributes.SetInt(key, value);
            player.Entity.WatchedAttributes.MarkPathDirty(key);
        }
    }
}
