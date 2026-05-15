using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Utility for evaluating a player's currently equipped armor tier.
    /// Returns the highest protection tier across all worn armor pieces.
    /// </summary>
    public static class TrialArmorTierUtils
    {
        /// <summary>
        /// Get the maximum protection tier across all currently worn armor.
        /// Returns 0 if no armor is worn.
        /// </summary>
        public static int GetMaxArmorTier(IServerPlayer player)
        {
            if (player?.Entity == null) return 0;

            var inv = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (inv == null) return 0;

            int maxTier = 0;
            foreach (var slot in inv)
            {
                if (slot?.Itemstack?.Collectible == null || slot.Empty) continue;

                // Try IWearableStatsSupplier interface (includes ArmorBody/Head/Legs)
                var stats = slot.Itemstack.Collectible.GetCollectibleInterface<IWearableStatsSupplier>();
                if (stats != null && stats.IsArmorType(slot))
                {
                    var prot = stats.GetProtectionModifiers(slot);
                    if (prot != null && prot.ProtectionTier > maxTier)
                    {
                        maxTier = prot.ProtectionTier;
                    }
                    continue;
                }

                // Fallback: read from item attributes
                var attrs = slot.Itemstack.Collectible.Attributes;
                if (attrs != null)
                {
                    int tier = attrs["protectionModifiers"]?["protectionTier"].AsInt(0) ?? 0;
                    if (tier > maxTier) maxTier = tier;
                }
            }

            return maxTier;
        }
    }
}
