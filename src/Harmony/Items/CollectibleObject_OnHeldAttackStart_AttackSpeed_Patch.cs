using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;

namespace VsQuest.Harmony.Items
{
    /// <summary>
    /// Harmony patch to apply attack speed modifier to attack animations.
    /// This fixes the issue where attack speed stat doesn't affect actual attack rate
    /// because animations still play at normal speed.
    /// </summary>
    [HarmonyPatch(typeof(CollectibleObject), "OnHeldAttackStart")]
    public class CollectibleObject_OnHeldAttackStart_AttackSpeed_Patch
    {
        public static void Prefix(EntityAgent byEntity, ItemSlot slot, ref float __state)
        {
            if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_CollectibleObject_OnHeldAttackStart_AttackSpeed)) return;
            if (byEntity is not EntityPlayer player) return;

            // Store original attack animation speed - use default since we can't get current speed
            __state = 1f;
        }

        public static void Postfix(EntityAgent byEntity, ItemSlot slot, float __state)
        {
            if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_CollectibleObject_OnHeldAttackStart_AttackSpeed)) return;
            if (byEntity is not EntityPlayer player) return;
            if (slot?.Empty != false) return;

            // Only apply to melee weapons, not ranged weapons
            // Ranged weapons use rangedchargspeed attribute instead
            if (IsRangedWeapon(slot.Itemstack)) return;

            // Get attack speed from stats
            float attackSpeedBonus = player.Stats.GetBlended("attackSpeed");
            if (Math.Abs(attackSpeedBonus) < 0.0001f) return;

            // Apply attack speed modifier to animation
            // Positive attackSpeed makes attacks faster (higher animation speed)
            // Negative attackSpeed makes attacks slower (lower animation speed)
            float speedMultiplier = 1f + attackSpeedBonus;
            speedMultiplier = GameMath.Clamp(speedMultiplier, 0.1f, 5f); // Prevent extreme values

            // Store the speed multiplier in entity attributes for the animation system to use
            player.WatchedAttributes.SetFloat("vsquest:attackSpeedMultiplier", speedMultiplier);
        }

        private static bool IsRangedWeapon(ItemStack stack)
        {
            if (stack?.Collectible == null) return false;
            
            var collectible = stack.Collectible;
            var code = collectible.Code?.Path ?? "";
            
            // Check if it's a bow, crossbow, or other ranged weapon by path
            return code.Contains("bow") || 
                   code.Contains("crossbow") || 
                   code.Contains("arrow") || 
                   code.Contains("spear") || 
                   code.Contains("javelin") ||
                   code.Contains("throwing") ||
                   code.Contains("ranged");
        }
    }
}
