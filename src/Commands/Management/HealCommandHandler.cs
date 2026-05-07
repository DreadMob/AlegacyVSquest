using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class HealCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public HealCommandHandler(ICoreServerAPI sapi)
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
                // Get health tree
                var healthTree = targetEntity.WatchedAttributes?.GetTreeAttribute("health");
                if (healthTree == null)
                {
                    return TextCommandResult.Error("Player does not have health behavior.");
                }

                float maxHealth = healthTree.GetFloat("maxhealth", 15f);
                
                // Heal to full health
                healthTree.SetFloat("currenthealth", maxHealth);
                targetEntity.WatchedAttributes.MarkPathDirty("health");

                string name = targetEntity.Player?.PlayerName ?? "Player";
                return TextCommandResult.Success($"{name} has been healed to full health ({maxHealth} HP).");
            }
            catch (Exception e)
            {
                try
                {
                    sapi?.Logger?.Error("[alegacyvsquest] /avq heal failed: {0}", e);
                }
                catch (Exception)
                {
                    // Swallow - logging failed
                }

                return TextCommandResult.Error($"Failed to heal player: {e.Message}");
            }
        }
    }
}
