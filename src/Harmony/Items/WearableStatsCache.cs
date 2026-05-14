using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VsQuest.Harmony.Items
{
    /// <summary>
    /// Cached wearable stats to avoid recalculating on every stat check.
    /// Uses time-based expiration (500ms) instead of invalidation.
    /// </summary>
    public static class WearableStatsCache
    {
        private const string CacheKey = "vsquest:wearablestatscache:v3"; // v3: combined storage
        private const int CacheValidityMs = 500; // Cache valid for 500ms

        public class CachedStats
        {
            public float Stealth { get; set; }
            public float FallDamageReduction { get; set; }
            public float TemporalDrainReduction { get; set; }
            public float MiningSpeedMult { get; set; }
            public float FlatProtection { get; set; }
            public float KnockbackMult { get; set; }
            public float AttackPower { get; set; }
            public float MeleeAttackPower { get; set; }
            public float AttackSpeed { get; set; }
            public float RangedDamageMult { get; set; }
            public long Timestamp { get; set; }

            // Fast check if cache is still valid
            public bool IsValid(long nowMs) => nowMs > 0 && Timestamp > 0 && (nowMs - Timestamp) < CacheValidityMs;
        }

        public static CachedStats GetCachedStats(EntityPlayer player)
        {
            if (player?.Player?.InventoryManager == null) return null;
            var inv = player.Player.InventoryManager.GetOwnInventory("character");
            var backpackInv = player.Player.InventoryManager.GetOwnInventory("backpack");
            if (inv == null && backpackInv == null) return null;

            long nowMs = player.World?.ElapsedMilliseconds ?? 0;

            // Try get cached stats in single read
            var cached = GetStatsFromCache(player, nowMs);
            if (cached != null) return cached;

            // Recalculate and cache
            var stats = CalculateStats(inv, backpackInv);
            stats.Timestamp = nowMs;
            StoreStatsInCache(player, stats);
            return stats;
        }

        public static void InvalidateCache(EntityPlayer player)
        {
            // Set timestamp to 0 to invalidate without clearing data
            var tree = player.WatchedAttributes?.GetTreeAttribute(CacheKey);
            tree?.SetLong("t", 0);
        }

        private static CachedStats CalculateStats(IInventory inv, IInventory backpackInv)
        {
            var stats = new CachedStats();

            // Check character slots for items with attributes (ItemWearable or action items)
            if (inv != null)
            {
                foreach (ItemSlot slot in inv)
                {
                    if (slot.Empty || slot.Itemstack?.Attributes == null) continue;
                    AddItemAttributes(stats, slot.Itemstack);
                }
            }

            // Check all 4 backpack slots - each can hold a bag/quiver
            // Other slots contain items STORED INSIDE the bags - we should NOT apply their stats
            if (backpackInv != null)
            {
                for (int i = 0; i < backpackInv.Count && i < 4; i++)
                {
                    var bagSlot = backpackInv[i];
                    if (!bagSlot.Empty && bagSlot.Itemstack?.Attributes != null)
                    {
                        AddItemAttributes(stats, bagSlot.Itemstack);
                    }
                }
            }

            return stats;
        }

        private static void AddItemAttributes(CachedStats stats, ItemStack stack)
        {
            // Ensure action item attributes are applied from itemconfig.json
            EnsureItemAttributes(stack);

            stats.Stealth += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrStealth);
            stats.FallDamageReduction += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrFallDamageMult);
            stats.TemporalDrainReduction += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrTemporalDrainMult);
            stats.MiningSpeedMult += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrMiningSpeedMult);
            stats.FlatProtection += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrProtection);
            stats.KnockbackMult += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrKnockbackMult);
            stats.AttackPower += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrAttackPower);
            stats.MeleeAttackPower += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrMeleeAttackPower);
            stats.AttackSpeed += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrAttackSpeed);
            stats.RangedDamageMult += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrRangedDamageMult);
        }

        public static void EnsureItemAttributes(ItemStack stack)
        {
            if (stack?.Attributes == null) return;
            if (ItemAttributeUtils.IsActionItem(stack)) return; // Already has action item attributes

            var registry = ItemSystem.StaticActionItemRegistry;
            if (registry == null || registry.Count == 0) return;

            // Try to find by itemCode and apply attributes
            if (stack.Collectible?.Code == null) return;
            string code = stack.Collectible.Code.ToString();

            ActionItem found = null;
            foreach (var entry in registry.Values)
            {
                if (entry?.itemCode == null) continue;
                if (string.Equals(entry.itemCode, code, System.StringComparison.OrdinalIgnoreCase))
                {
                    found = entry;
                    break;
                }
            }

            if (found != null)
            {
                ItemAttributeUtils.ApplyActionItemAttributes(stack, found);
            }
        }

        private static CachedStats GetStatsFromCache(EntityPlayer player, long nowMs)
        {
            try
            {
                var tree = player.WatchedAttributes?.GetTreeAttribute(CacheKey);
                if (tree == null) return null;

                long timestamp = tree.GetLong("t");
                // Check validity inline to avoid second WatchedAttributes read
                if (nowMs > 0 && timestamp > 0 && (nowMs - timestamp) < CacheValidityMs)
                {
                    return new CachedStats
                    {
                        Stealth = tree.GetFloat("s"),
                        FallDamageReduction = tree.GetFloat("f"),
                        TemporalDrainReduction = tree.GetFloat("tp"),
                        MiningSpeedMult = tree.GetFloat("m"),
                        FlatProtection = tree.GetFloat("pf"),
                        KnockbackMult = tree.GetFloat("k"),
                        AttackPower = tree.GetFloat("ap"),
                        MeleeAttackPower = tree.GetFloat("mp"),
                        AttackSpeed = tree.GetFloat("as"),
                        RangedDamageMult = tree.GetFloat("rd"),
                        Timestamp = timestamp
                    };
                }
                return null;
            }
            catch { return null; }
        }

        private static void StoreStatsInCache(EntityPlayer player, CachedStats stats)
        {
            try
            {
                var tree = new TreeAttribute();
                // Short keys for smaller serialization
                tree.SetFloat("s", stats.Stealth);
                tree.SetFloat("f", stats.FallDamageReduction);
                tree.SetFloat("tp", stats.TemporalDrainReduction);
                tree.SetFloat("m", stats.MiningSpeedMult);
                tree.SetFloat("pf", stats.FlatProtection);
                tree.SetFloat("k", stats.KnockbackMult);
                tree.SetFloat("ap", stats.AttackPower);
                tree.SetFloat("mp", stats.MeleeAttackPower);
                tree.SetFloat("as", stats.AttackSpeed);
                tree.SetFloat("rd", stats.RangedDamageMult);
                tree.SetLong("t", stats.Timestamp);
                player.WatchedAttributes.SetAttribute(CacheKey, tree);
            }
            catch (Exception)
            {
                // Swallow - attribute set may fail
            }
        }
    }
}
