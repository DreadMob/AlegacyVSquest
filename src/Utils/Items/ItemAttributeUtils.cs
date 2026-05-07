using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Newtonsoft.Json;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace VsQuest
{
    public static class ItemAttributeUtils
    {
        public const string AttrPrefix = "alegacyvsquest:attr:";

        public const string ActionItemActionsKey = "alegacyvsquest:actions";
        public const string ActionItemIdKey = "alegacyvsquest:actionitemid";
        public const string ActionItemSourceQuestKey = "alegacyvsquest:sourcequest";
        public const string ActionItemDefaultSourceQuestId = "item-action";
        public const string ActionItemModesKey = "alegacyvsquest:modes";
        public const string ActionItemModeIndexKey = "alegacyvsquest:mode";
        public const string ActionItemTriggerOnInvAddKey = "alegacyvsquest:triggerOnInvAdd";
        public const string ActionItemBlockMoveKey = "alegacyvsquest:blockMove";     // restrict movement (hotbar-only)
        public const string ActionItemBlockEquipKey = "alegacyvsquest:blockEquip";   // restrict equipping (character slots)
        public const string ActionItemBlockDropKey = "alegacyvsquest:blockDrop";     // restrict manual drop
        public const string ActionItemBlockDeathKey = "alegacyvsquest:blockDeath";   // restrict drop on death
        public const string ActionItemBlockGroundStorageKey = "alegacyvsquest:blockGroundStorage"; // restrict Shift+RightClick ground storage placement
        public const string ActionItemShowAttrsKey = "alegacyvsquest:showAttrs";
        public const string ActionItemHideVanillaKey = "alegacyvsquest:hideVanilla";

        public const string QuestNameKey = "alegacyvsquest:questName";
        public const string QuestDescKey = "alegacyvsquest:questDesc";

        // Item quality keys
        public const string ItemQualityIdKey = "alegacyvsquest:qualityId";
        public const string ItemQualityNameKey = "alegacyvsquest:qualityName";
        public const string ItemQualityColorKey = "alegacyvsquest:qualityColor";
        public const string ItemQualityBonusPercentKey = "alegacyvsquest:qualityBonusPercent";
        public const string ItemQualityBonusDataKey = "alegacyvsquest:qualityBonusData";

        public const string AttrAttackPower = "attackpower";
        public const string AttrWarmth = "warmth";
        public const string AttrProtection = "protection";
        public const string AttrWalkSpeed = "walkspeed";
        public const string AttrHungerRate = "hungerrate";
        public const string AttrHealingEffectiveness = "healingeffectiveness";
        public const string AttrRangedAccuracy = "rangedaccuracy";
        public const string AttrRangedSpeed = "rangedchargspeed";
        public const string AttrRangedDamageMult = "rangeddamagemult";
        public const string AttrMiningSpeedMult = "miningspeedmult";
        public const string AttrFallDamageMult = "falldamagemult";
        public const string AttrTemporalDrainMult = "temporaldrainmult";
        public const string AttrJumpHeightMul = "jumpheightmul";
        public const string AttrKnockbackMult = "knockbackmult";
        public const string AttrMaxHealthFlat = "maxhealthflat";
        public const string AttrMaxOxygen = "maxoxygen";
        public const string AttrStealth = "stealth";
        public const string AttrSecondChanceCharges = "secondchancecharges";
        public const string AttrWeightLimit = "weightlimit";
        public const string AttrViewDistance = "viewdistance";
        public const string AttrUraniumMaskChargeHours = "uraniummaskchargehours";
        public const string AttrHealOnKill = "healonkill";

        public static string GetKey(string attributeName)
        {
            return AttrPrefix + attributeName;
        }

        public static float GetAttributeFloat(ItemStack stack, string attributeName, float defaultValue = 0f)
        {
            if (stack == null || stack.Attributes == null) return defaultValue;

            string key = GetKey(attributeName);
            if (stack.Attributes.HasAttribute(key))
            {
                return stack.Attributes.GetFloat(key, defaultValue);
            }

            return defaultValue;
        }

        public static float GetConditionMultiplier(ItemStack stack)
        {
            if (stack?.Collectible == null) return 1f;

            const float FullEffectUntil = 0.6f;

            if (stack.Attributes != null && stack.Attributes.HasAttribute("condition"))
            {
                float condition = GameMath.Clamp(stack.Attributes.GetFloat("condition", 1f), 0f, 1f);
                if (condition >= FullEffectUntil) return 1f;
                return FullEffectUntil <= 0f ? 0f : GameMath.Clamp(condition / FullEffectUntil, 0f, 1f);
            }

            int maxDurability = stack.Collectible.GetMaxDurability(stack);
            if (maxDurability > 0)
            {
                int remaining = stack.Collectible.GetRemainingDurability(stack);
                float condition = GameMath.Clamp(remaining / (float)maxDurability, 0f, 1f);
                if (condition >= FullEffectUntil) return 1f;
                return FullEffectUntil <= 0f ? 0f : GameMath.Clamp(condition / FullEffectUntil, 0f, 1f);
            }

            return 1f;
        }

        public static float GetAttributeFloatScaled(ItemStack stack, string attributeName, float defaultValue = 0f)
        {
            float value = GetAttributeFloat(stack, attributeName, defaultValue);
            if (value == 0f || stack == null) return value;

            // Some wearables have effects that should only apply when they are charged.
            // If the charge is depleted, suppress all other attribute bonuses from that wearable.
            float uraniumMaskChargeHours = GetAttributeFloat(stack, AttrUraniumMaskChargeHours, float.NaN);
            if (!float.IsNaN(uraniumMaskChargeHours))
            {
                if (attributeName != AttrUraniumMaskChargeHours && uraniumMaskChargeHours <= 0f)
                {
                    return 0f;
                }

                // Charged mask bonuses should not depend on item durability/condition.
                // The only controlling factor should be the time-based charge.
                if (attributeName == AttrUraniumMaskChargeHours) return value;

                // Scale effects down when charge is low.
                // >= 24h => full power (1.0)
                // < 24h  => scales linearly down to 0.4
                float chargeMult = uraniumMaskChargeHours >= 24f
                    ? 1f
                    : GameMath.Clamp(0.4f + 0.6f * (uraniumMaskChargeHours / 24f), 0.4f, 1f);

                return value * chargeMult;
            }

            if (attributeName == AttrHungerRate)
            {
                // Negative hungerrate (hunger reduction bonus) should scale with condition
                // and go to 0 when item is broken. Positive hungerrate (debuff) stays constant.
                if (value < 0f)
                {
                    float hungerMult = GetConditionMultiplier(stack);
                    // At condition 0, mult should be 0, so negative bonus disappears
                    hungerMult = GameMath.Clamp(hungerMult, 0f, 1f);
                    return value * hungerMult;
                }
                return value;
            }

            // Debuffs should not scale with item condition/durability.
            // Only positive bonuses are reduced when the item is in poor condition.
            if (value < 0f) return value;

            float mult = GetConditionMultiplier(stack);
            mult = GameMath.Clamp(mult, 0.3f, 1f);
            return value * mult;
        }

        public static string GetDisplayName(string shortKey)
        {
            string langKey = $"alegacyvsquest:attr-{shortKey}";
            string result = Lang.Get(langKey);

            if (result == langKey)
            {
                return shortKey;
            }
            return result;
        }

        public static string FormatAttributeForTooltip(string attrKey, float value)
        {
            string shortKey = attrKey.StartsWith(AttrPrefix) ? attrKey.Substring(AttrPrefix.Length) : attrKey;
            string displayName = GetDisplayName(shortKey);

            string prefix = value >= 0 ? "+" : "";

            if (shortKey == AttrWalkSpeed ||
                shortKey == AttrHungerRate || shortKey == AttrHealingEffectiveness ||
                shortKey == AttrRangedAccuracy || shortKey == AttrRangedSpeed || shortKey == AttrRangedDamageMult ||
                shortKey == AttrMiningSpeedMult || shortKey == AttrFallDamageMult ||
                shortKey == AttrTemporalDrainMult || shortKey == AttrJumpHeightMul ||
                shortKey == AttrKnockbackMult || shortKey == AttrWeightLimit ||
                shortKey == AttrViewDistance)
            {
                return $"{displayName}: {prefix}{value * 100:0.#}%";
            }
            else if (shortKey == AttrWarmth)
            {
                return $"{displayName}: {prefix}{value:0.#}°C";
            }
            else if (shortKey == AttrAttackPower)
            {
                return $"{displayName}: {prefix}{value:0.#} hp";
            }
            else if (shortKey == AttrProtection)
            {
                return $"{displayName}: {prefix}{value:0.#} dmg";
            }
            else if (shortKey == AttrSecondChanceCharges)
            {
                return $"{displayName}: {value:0.#}";
            }
            else if (shortKey == AttrUraniumMaskChargeHours)
            {
                return $"{displayName}: {value:0.#}h";
            }
            else if (shortKey == AttrMaxHealthFlat)
            {
                return $"{displayName}: {prefix}{value:0.#} hp";
            }
            else if (shortKey == AttrMaxOxygen)
            {
                const float OxygenUnitsPerSecond = 800f;
                float seconds = value / OxygenUnitsPerSecond;
                return $"{displayName}: {prefix}{seconds:0.#}";
            }
            else if (shortKey == AttrHealOnKill)
            {
                return $"{displayName}: {prefix}{value * 100:0.#}%";
            }

            return $"{displayName}: {prefix}{value:0.##}";
        }

        /// <summary>
        /// Formats an attribute with its quality bonus for tooltip display.
        /// Example: "Атака: +2.5 hp (+0.5 hp)" or "Скорость: -14% (+3%)"
        /// </summary>
        public static string FormatAttributeWithBonus(string attrKey, float value, float bonus, string qualityColor)
        {
            string shortKey = attrKey.StartsWith(AttrPrefix) ? attrKey.Substring(AttrPrefix.Length) : attrKey;
            string displayName = GetDisplayName(shortKey);

            string prefix = value >= 0 ? "+" : "";
            string bonusPrefix = bonus >= 0 ? "+" : "";
            string bonusColored = $"<font color=\"{qualityColor}\">({bonusPrefix}{bonus:0.#})</font>";

            if (shortKey == AttrWalkSpeed ||
                shortKey == AttrHungerRate || shortKey == AttrHealingEffectiveness ||
                shortKey == AttrRangedAccuracy || shortKey == AttrRangedSpeed || shortKey == AttrRangedDamageMult ||
                shortKey == AttrMiningSpeedMult || shortKey == AttrFallDamageMult ||
                shortKey == AttrTemporalDrainMult || shortKey == AttrJumpHeightMul ||
                shortKey == AttrKnockbackMult || shortKey == AttrWeightLimit ||
                shortKey == AttrViewDistance || shortKey == AttrHealOnKill)
            {
                // Percentage attributes
                string bonusFormatted = $"<font color=\"{qualityColor}\">({bonusPrefix}{bonus * 100:0.#}%)</font>";
                return $"{displayName}: {prefix}{value * 100:0.#}% {bonusFormatted}";
            }
            else if (shortKey == AttrWarmth)
            {
                string bonusFormatted = $"<font color=\"{qualityColor}\">({bonusPrefix}{bonus:0.#}°C)</font>";
                return $"{displayName}: {prefix}{value:0.#}°C {bonusFormatted}";
            }
            else if (shortKey == AttrAttackPower || shortKey == AttrMaxHealthFlat)
            {
                string bonusFormatted = $"<font color=\"{qualityColor}\">({bonusPrefix}{bonus:0.#} hp)</font>";
                return $"{displayName}: {prefix}{value:0.#} hp {bonusFormatted}";
            }
            else if (shortKey == AttrProtection)
            {
                string bonusFormatted = $"<font color=\"{qualityColor}\">({bonusPrefix}{bonus:0.#} dmg)</font>";
                return $"{displayName}: {prefix}{value:0.#} dmg {bonusFormatted}";
            }
            else if (shortKey == AttrMaxOxygen)
            {
                const float OxygenUnitsPerSecond = 800f;
                float seconds = value / OxygenUnitsPerSecond;
                float bonusSeconds = bonus / OxygenUnitsPerSecond;
                string bonusFormatted = $"<font color=\"{qualityColor}\">({bonusPrefix}{bonusSeconds:0.#})</font>";
                return $"{displayName}: {prefix}{seconds:0.#} {bonusFormatted}";
            }
            else if (shortKey == AttrSecondChanceCharges || shortKey == AttrUraniumMaskChargeHours)
            {
                // These don't typically get bonuses, but handle them anyway
                string unit = shortKey == AttrUraniumMaskChargeHours ? "h" : "";
                string bonusFormatted = $"<font color=\"{qualityColor}\">({bonusPrefix}{bonus:0.#}{unit})</font>";
                return $"{displayName}: {value:0.#}{unit} {bonusFormatted}";
            }

            // Default format
            return $"{displayName}: {prefix}{value:0.##} {bonusColored}";
        }

        public static bool IsActionItem(ItemStack stack)
        {
            if (stack?.Attributes == null) return false;
            var actions = stack.Attributes.GetString(ActionItemActionsKey);
            return !string.IsNullOrWhiteSpace(actions);
        }

        public static bool IsActionItemBlockedMove(ItemStack stack)
        {
            if (stack?.Attributes == null) return false;
            return stack.Attributes.GetBool(ActionItemBlockMoveKey, false) && IsActionItem(stack);
        }

        public static bool IsActionItemBlockedEquip(ItemStack stack)
        {
            if (stack?.Attributes == null) return false;
            return stack.Attributes.GetBool(ActionItemBlockEquipKey, false) && IsActionItem(stack);
        }

        public static bool IsActionItemBlockedDrop(ItemStack stack)
        {
            if (stack?.Attributes == null) return false;
            return stack.Attributes.GetBool(ActionItemBlockDropKey, false) && IsActionItem(stack);
        }

        public static bool IsActionItemBlockedDeath(ItemStack stack)
        {
            if (stack?.Attributes == null) return false;
            return stack.Attributes.GetBool(ActionItemBlockDeathKey, false) && IsActionItem(stack);
        }

        public static bool IsActionItemBlockedGroundStorage(ItemStack stack)
        {
            if (stack?.Attributes == null) return false;
            return stack.Attributes.GetBool(ActionItemBlockGroundStorageKey, false) && IsActionItem(stack);
        }

        public static bool TryResolveCollectible(ICoreAPI api, string itemCode, out CollectibleObject collectible)
        {
            collectible = null;
            if (api?.World == null) return false;
            if (string.IsNullOrWhiteSpace(itemCode)) return false;

            collectible = api.World.GetItem(new AssetLocation(itemCode));
            if (collectible == null)
            {
                collectible = api.World.GetBlock(new AssetLocation(itemCode));
            }

            return collectible != null && !collectible.IsMissing;
        }

        public static void ApplyActionItemAttributes(ItemStack stack, ActionItem actionItem)
        {
            if (stack == null || actionItem == null) return;

            if (stack.Attributes == null)
            {
                stack.Attributes = new TreeAttribute();
            }

            if (!string.IsNullOrWhiteSpace(actionItem.name))
            {
                stack.Attributes.SetString(QuestNameKey, actionItem.name);
            }
            if (!string.IsNullOrWhiteSpace(actionItem.description))
            {
                stack.Attributes.SetString(QuestDescKey, actionItem.description);
            }

            stack.Attributes.SetString(ActionItemActionsKey, JsonConvert.SerializeObject(actionItem.actions));

            if (!string.IsNullOrWhiteSpace(actionItem.id))
            {
                stack.Attributes.SetString(ActionItemIdKey, actionItem.id);
            }

            if (actionItem.modes != null && actionItem.modes.Count > 0)
            {
                stack.Attributes.SetString(ActionItemModesKey, JsonConvert.SerializeObject(actionItem.modes));
                stack.Attributes.SetInt(ActionItemModeIndexKey, 0);
            }

            if (!string.IsNullOrWhiteSpace(actionItem.sourceQuestId))
            {
                stack.Attributes.SetString(ActionItemSourceQuestKey, actionItem.sourceQuestId);
            }

            if (actionItem.triggerOnInventoryAdd)
            {
                stack.Attributes.SetBool(ActionItemTriggerOnInvAddKey, true);
            }
            else
            {
                stack.Attributes.RemoveAttribute(ActionItemTriggerOnInvAddKey);
            }

            if (actionItem.blockMove)
            {
                stack.Attributes.SetBool(ActionItemBlockMoveKey, true);
            }
            else
            {
                stack.Attributes.RemoveAttribute(ActionItemBlockMoveKey);
            }

            if (actionItem.blockEquip)
            {
                stack.Attributes.SetBool(ActionItemBlockEquipKey, true);
            }
            else
            {
                stack.Attributes.RemoveAttribute(ActionItemBlockEquipKey);
            }

            if (actionItem.blockDrop)
            {
                stack.Attributes.SetBool(ActionItemBlockDropKey, true);
            }
            else
            {
                stack.Attributes.RemoveAttribute(ActionItemBlockDropKey);
            }

            if (actionItem.blockDeath)
            {
                stack.Attributes.SetBool(ActionItemBlockDeathKey, true);
            }
            else
            {
                stack.Attributes.RemoveAttribute(ActionItemBlockDeathKey);
            }

            if (actionItem.blockGroundStorage)
            {
                stack.Attributes.SetBool(ActionItemBlockGroundStorageKey, true);
            }
            else
            {
                stack.Attributes.RemoveAttribute(ActionItemBlockGroundStorageKey);
            }

            if (actionItem.attributes != null)
            {
                foreach (var attr in actionItem.attributes)
                {
                    stack.Attributes.SetFloat(GetKey(attr.Key), attr.Value);
                }
            }

            if (actionItem.showAttributes != null && actionItem.showAttributes.Count > 0)
            {
                stack.Attributes.SetString(ActionItemShowAttrsKey, JsonConvert.SerializeObject(actionItem.showAttributes));
            }

            if (actionItem.hideVanillaTooltips != null && actionItem.hideVanillaTooltips.Count > 0)
            {
                stack.Attributes.SetString(ActionItemHideVanillaKey, JsonConvert.SerializeObject(actionItem.hideVanillaTooltips));
            }
        }

        /// <summary>
        /// Computes a stable hash of item attributes for change detection.
        /// Float values that change frequently (like charge timers) are rounded to avoid jitter.
        /// </summary>
        public static int GetStableAttributeHash(ItemStack stack)
        {
            if (stack?.Attributes == null) return 0;

            int hash = 17;

            // Time-based charges - round to 0.5h precision to avoid jitter on every tick
            if (stack.Attributes.HasAttribute(GetKey(AttrUraniumMaskChargeHours)))
            {
                float charge = stack.Attributes.GetFloat(GetKey(AttrUraniumMaskChargeHours));
                int rounded = (int)System.Math.Round(charge * 2);
                hash = hash * 31 + rounded;
            }

            // Integer-based charges - use directly
            if (stack.Attributes.HasAttribute(GetKey(AttrSecondChanceCharges)))
            {
                float charges = stack.Attributes.GetFloat(GetKey(AttrSecondChanceCharges));
                hash = hash * 31 + charges.GetHashCode();
            }

            // Condition affects stats - include with rounding
            if (stack.Attributes.HasAttribute("condition"))
            {
                float condition = stack.Attributes.GetFloat("condition", 1f);
                int rounded = (int)System.Math.Round(condition * 100); // Round to 1% precision
                hash = hash * 31 + rounded;
            }

            return hash;
        }
    }
}
