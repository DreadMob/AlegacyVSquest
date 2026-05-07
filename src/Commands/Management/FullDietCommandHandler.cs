using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class FullDietCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public FullDietCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            string playerName = null;
            if (args.ArgCount > 0)
            {
                playerName = args[0] as string;
            }

            EntityPlayer targetEntity = null;

            if (!string.IsNullOrWhiteSpace(playerName))
            {
                var player = sapi.World.PlayerByUid(playerName);
                if (player == null)
                {
                    // Try by name
                    foreach (var p in sapi.World.AllOnlinePlayers)
                    {
                        if (string.Equals(p.PlayerName, playerName, StringComparison.OrdinalIgnoreCase))
                        {
                            player = p;
                            break;
                        }
                    }
                }
                targetEntity = player?.Entity as EntityPlayer;
            }
            else
            {
                // Use caller
                targetEntity = args.Caller?.Entity as EntityPlayer;
            }

            if (targetEntity == null)
            {
                return TextCommandResult.Error("Player not found.");
            }

            try
            {
                var bh = targetEntity.GetBehavior<EntityBehaviorHunger>();
                if (bh == null)
                {
                    return TextCommandResult.Error("Player does not have hunger behavior.");
                }

                // Set saturation and nutrition to full
                var hungerTree = targetEntity.WatchedAttributes?.GetTreeAttribute("hunger");
                if (hungerTree != null)
                {
                
                
                
                
                
                
            
                    float maxSaturation = hungerTree.GetFloat("maxsaturation", 150f);

                    // Set current saturation to max
                    hungerTree.SetFloat("currentsaturation", maxSaturation);

                    // Set nutrition levels to max (stored in hunger tree)
                    hungerTree.SetFloat("fruitLevel", maxSaturation);
                    hungerTree.SetFloat("vegetableLevel", maxSaturation);
                    hungerTree.SetFloat("grainLevel", maxSaturation);
                    hungerTree.SetFloat("proteinLevel", maxSaturation);
                    hungerTree.SetFloat("dairyLevel", maxSaturation);

                    targetEntity.WatchedAttributes.MarkPathDirty("hunger");
                }

                string name = targetEntity.Player?.PlayerName ?? "Player";
                return TextCommandResult.Success($"{name}'s diet has been restored to full nutrition.");
            }
            catch (Exception e)
            {
                try
                {
                    sapi?.Logger?.Error("[alegacyvsquest] /avq fulldiet failed: {0}", e);
                }
                catch (Exception)
                {
                    // Swallow - logging failed
                }

                return TextCommandResult.Error($"Failed to set full diet: {e.Message}");
            }
        }
    }
}
