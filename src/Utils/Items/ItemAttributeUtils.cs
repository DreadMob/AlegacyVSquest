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
        public const string ActionItemRerollGroupKey = "alegacyvsquest:rerollGroup";
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
        public const string AttrMeleeAttackPower = "meleeattackpower";
        public const string AttrAttackSpeed = "attackspeed";
        public const string AttrDamageTier = "damagetier";
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
        public const string AttrFurCoatChargeHours = "furcoatchargehours";

        // Charge system metadata keys (stored on item stack attributes, not prefixed)
        /// <summary>
        /// Charge mode: "all" = all stats gated by charge (default), "partial" = only listed attrs gated.
        /// </summary>
        public const string ChargeMode = "alegacyvsquest:chargemode";
        /// <summary>
        /// JSON array of attribute short keys that are gated by charge (used when chargemode = "partial").
        /// </summary>
        public const string ChargeGatedAttrs = "alegacyvsquest:chargegatedattrs";
        /// <summary>
        /// JSON array of item code substrings accepted as charge materials.
        /// </summary>
        public const string ChargeMaterials = "alegacyvsquest:chargematerials";
        /// <summary>
        /// Hours of charge added per unit of material.
        /// </summary>
        public const string ChargePerUnit = "alegacyvsquest:chargeperunit";
        /// <summary>
        /// Maximum charge capacity in hours.
        /// </summary>
        public const string ChargeMaxHours = "alegacyvsquest:chargemax";
        /// <summary>
        /// Multiplier for gated attributes when charge is fully depleted (0 = no effect, 0.267 = 26.7%).
        /// </summary>
        public const string ChargeDepletedMult = "alegacyvsquest:chargedepletedmult";

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

            // --- Unified charge system ---
            // Find any *chargehours attribute on this item. If present, apply charge gating logic.
            float chargeHours = float.NaN;
            string chargeAttrName = null;
            var treeAttrs = stack.Attributes as TreeAttribute;
            if (treeAttrs != null)
            {
                foreach (var val in treeAttrs)
                {
                    if (val.Key.EndsWith("chargehours"))
                    {
                        chargeAttrName = val.Key.StartsWith(AttrPrefix)
                            ? val.Key.Substring(AttrPrefix.Length)
                            : val.Key;
                        chargeHours = stack.Attributes.GetFloat(val.Key, 0f);
                        break;
                    }
                }
            }

            if (!float.IsNaN(chargeHours))
            {
                // The charge attribute itself is always returned as-is
                if (attributeName == chargeAttrName) return value;

                // Determine charge mode: "all" (default) or "partial"
                string mode = stack.Attributes.GetString(ChargeMode, "all");

                bool isGated;
                if (mode == "partial")
                {
                    // Only specific attributes are gated by charge
                    string gatedJson = stack.Attributes.GetString(ChargeGatedAttrs, "[]");
                    isGated = gatedJson.Contains("\"" + attributeName + "\"");
                }
                else
                {
                    // "all" mode: every attribute except the charge attr itself is gated
                    isGated = true;
                }

                if (isGated)
                {
                    if (chargeHours <= 0f)
                    {
                        // When depleted, apply minimum multiplier (default 0 = no effect)
                        float depletedMult = stack.Attributes.GetFloat(ChargeDepletedMult, 0f);
                        return value * depletedMult;
                    }

                    // Scale effects down when charge is low.
                    // >= 24h => full power (1.0)
                    // < 24h  => scales linearly down to 0.4
                    float chargeMult = chargeHours >= 24f
                        ? 1f
                        : GameMath.Clamp(0.4f + 0.6f * (chargeHours / 24f), 0.4f, 1f);
                    return value * chargeMult;
                }
                // Non-gated attributes fall through to normal condition logic below.
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
            // 1. Try nested localization system
            string langKey = $"alegacyvsquest:attr-{shortKey}";
            string nested = LocalizationUtils.GetFromNested(langKey);
            if (!string.IsNullOrEmpty(nested)) return nested;
            
            nested = LocalizationUtils.GetFromNested($"attr-{shortKey}");
            if (!string.IsNullOrEmpty(nested)) return nested;
            
            // 2. Try vanilla Lang.Get
            string result = Lang.Get(langKey);
            if (result != langKey) return result;
            
            return shortKey;
        }

        private const string ColorPositive = "#4ADE80"; // green
        private const string ColorNegative = "#F87171"; // red
        private const string ColorNeutral = "#FBBF24"; // yellow/amber for special/neutral stats
        private const string ColorLabel = "#D1D5DB"; // gray for labels
        public const string SeparatorLine = "<font color=\"#6B7280\">\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC</font>";
        public const string SectionBuffs = "<font color=\"#4ADE80\">\u25AC\u25AC\u25AC\u25AC \u25B2\u25B2 \u0411\u043E\u043D\u0443\u0441\u044B \u25B2\u25B2 \u25AC\u25AC\u25AC</font>";
        public const string SectionDebuffs = "<font color=\"#F87171\">\u25AC\u25AC\u25AC\u25AC \u25BC\u25BC \u0428\u0442\u0440\u0430\u0444\u044B \u25BC\u25BC \u25AC\u25AC\u25AC</font>";
        public const string SectionDesc = "<font color=\"#6B7280\">\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC\u25AC</font>";

        // No per-line prefix symbols — only section headers have decoration
        public const string PrefixSpecial = "";
        public const string PrefixBuff = "";
        public const string PrefixDebuff = "";

        /// <summary>
        /// Determines if a given attribute is "special" (charges, timers — not a buff or debuff).
        /// </summary>
        public static bool IsSpecialAttribute(string shortKey)
        {
            return shortKey == AttrSecondChanceCharges || shortKey == AttrDamageTier || shortKey.EndsWith("chargehours");
        }

        /// <summary>
        /// Returns true if this attribute should NOT be scaled by quality bonuses.
        /// </summary>
        public static bool IsQualityExemptAttribute(string shortKey)
        {
            return shortKey == AttrDamageTier || shortKey == AttrSecondChanceCharges || shortKey.EndsWith("chargehours");
        }

        /// <summary>
        /// Determines if a given attribute value is "positive" (beneficial) for the player.
        /// Some attributes are inverted: e.g. hungerrate positive = debuff, negative = buff.
        /// </summary>
        public static bool IsValueBeneficial(string shortKey, float value)
        {
            if (value == 0f) return false;

            // Inverted attributes: positive value = penalty, negative = bonus
            if (shortKey == AttrHungerRate || shortKey == AttrTemporalDrainMult || shortKey == AttrFallDamageMult)
            {
                return value < 0;
            }
            return value > 0;
        }

        /// <summary>
        /// Returns true if the value represents a debuff for this attribute.
        /// </summary>
        public static bool IsValueDebuff(string shortKey, float value)
        {
            if (value == 0f) return false;
            return !IsValueBeneficial(shortKey, value);
        }

        private static string ColorizeValue(string shortKey, float value, string formattedValue)
        {
            if (IsSpecialAttribute(shortKey))
                return $"<font color=\"{ColorNeutral}\">{formattedValue}</font>";
            if (value == 0f) return $"<font color=\"{ColorLabel}\">{formattedValue}</font>";
            string color = IsValueBeneficial(shortKey, value) ? ColorPositive : ColorNegative;
            return $"<font color=\"{color}\">{formattedValue}</font>";
        }

        private static string GetUnitSuffix(string shortKey)
        {
            string langKey = $"alegacyvsquest:attr-unit-{shortKey}";
            string result = Lang.Get(langKey);
            if (result != langKey) return result;

            // Fallback units
            if (shortKey == AttrAttackPower || shortKey == AttrMeleeAttackPower || shortKey == AttrMaxHealthFlat)
                return Lang.Get("alegacyvsquest:attr-unit-hp");
            if (shortKey == AttrProtection)
                return Lang.Get("alegacyvsquest:attr-unit-dmg");
            if (shortKey == AttrWarmth)
                return "°C";
            return "";
        }

        public static string FormatAttributeForTooltip(string attrKey, float value)
        {
            string shortKey = attrKey.StartsWith(AttrPrefix) ? attrKey.Substring(AttrPrefix.Length) : attrKey;
            string displayName = GetDisplayName(shortKey);

            string prefix = value >= 0 ? "+" : "";
            string formattedValue;

            if (IsSpecialAttribute(shortKey))
            {
                string unit = shortKey.EndsWith("chargehours") ? "ч" : "";
                if (shortKey == AttrDamageTier)
                {
                    formattedValue = $"{(int)value}";
                }
                else
                {
                    formattedValue = $"{value:0.#}{unit}";
                }
                return $"<font color=\"{ColorNeutral}\">{displayName}: {formattedValue}</font>";
            }

            if (shortKey == AttrWalkSpeed ||
                shortKey == AttrHungerRate || shortKey == AttrHealingEffectiveness ||
                shortKey == AttrRangedAccuracy || shortKey == AttrRangedSpeed || shortKey == AttrRangedDamageMult ||
                shortKey == AttrMiningSpeedMult || shortKey == AttrFallDamageMult ||
                shortKey == AttrTemporalDrainMult || shortKey == AttrJumpHeightMul ||
                shortKey == AttrKnockbackMult || shortKey == AttrWeightLimit ||
                shortKey == AttrViewDistance || shortKey == AttrHealOnKill ||
                shortKey == AttrStealth)
            {
                formattedValue = $"{prefix}{value * 100:0.#}%";
            }
            else if (shortKey == AttrWarmth)
            {
                formattedValue = $"{prefix}{value:0.#}°C";
            }
            else if (shortKey == AttrAttackPower || shortKey == AttrMeleeAttackPower || shortKey == AttrMaxHealthFlat)
            {
                string unit = Lang.Get("alegacyvsquest:attr-unit-hp");
                if (unit == "alegacyvsquest:attr-unit-hp") unit = "ед.";
                formattedValue = $"{prefix}{value:0.#} {unit}";
            }
            else if (shortKey == AttrProtection)
            {
                string unit = Lang.Get("alegacyvsquest:attr-unit-dmg");
                if (unit == "alegacyvsquest:attr-unit-dmg") unit = "ед.";
                formattedValue = $"{prefix}{value:0.#} {unit}";
            }
            else if (shortKey == AttrMaxOxygen)
            {
                const float OxygenUnitsPerSecond = 800f;
                float seconds = value / OxygenUnitsPerSecond;
                string unit = Lang.Get("alegacyvsquest:attr-unit-sec");
                if (unit == "alegacyvsquest:attr-unit-sec") unit = "сек";
                formattedValue = $"{prefix}{seconds:0.#} {unit}";
            }
            else
            {
                formattedValue = $"{prefix}{value:0.##}";
            }

            string coloredValue = ColorizeValue(shortKey, value, formattedValue);
            return $"<font color=\"{ColorLabel}\">{displayName}:</font>  {coloredValue}";
        }

        /// <summary>
        /// Formats an attribute with its quality bonus for tooltip display.
        /// </summary>
        public static string FormatAttributeWithBonus(string attrKey, float value, float bonus, string qualityColor)
        {
            string shortKey = attrKey.StartsWith(AttrPrefix) ? attrKey.Substring(AttrPrefix.Length) : attrKey;
            string displayName = GetDisplayName(shortKey);

            string prefix = value >= 0 ? "+" : "";
            string bonusPrefix = bonus >= 0 ? "+" : "";

            string formattedValue;
            string bonusFormatted;

            if (IsSpecialAttribute(shortKey))
            {
                string unit = shortKey.EndsWith("chargehours") ? "ч" : "";
                formattedValue = $"{value:0.#}{unit}";
                bonusFormatted = $"<font color=\"{qualityColor}\">({bonusPrefix}{bonus:0.#}{unit})</font>";
                return $"<font color=\"{ColorNeutral}\">{displayName}: {formattedValue}</font> {bonusFormatted}";
            }

            if (shortKey == AttrWalkSpeed ||
                shortKey == AttrHungerRate || shortKey == AttrHealingEffectiveness ||
                shortKey == AttrRangedAccuracy || shortKey == AttrRangedSpeed || shortKey == AttrRangedDamageMult ||
                shortKey == AttrMiningSpeedMult || shortKey == AttrFallDamageMult ||
                shortKey == AttrTemporalDrainMult || shortKey == AttrJumpHeightMul ||
                shortKey == AttrKnockbackMult || shortKey == AttrWeightLimit ||
                shortKey == AttrViewDistance || shortKey == AttrHealOnKill ||
                shortKey == AttrStealth)
            {
                formattedValue = $"{prefix}{value * 100:0.#}%";
                bonusFormatted = $"<font color=\"{qualityColor}\">({bonusPrefix}{bonus * 100:0.#}%)</font>";
            }
            else if (shortKey == AttrWarmth)
            {
                formattedValue = $"{prefix}{value:0.#}°C";
                bonusFormatted = $"<font color=\"{qualityColor}\">({bonusPrefix}{bonus:0.#}°C)</font>";
            }
            else if (shortKey == AttrAttackPower || shortKey == AttrMeleeAttackPower || shortKey == AttrMaxHealthFlat)
            {
                string unit = Lang.Get("alegacyvsquest:attr-unit-hp");
                if (unit == "alegacyvsquest:attr-unit-hp") unit = "ед.";
                formattedValue = $"{prefix}{value:0.#} {unit}";
                bonusFormatted = $"<font color=\"{qualityColor}\">({bonusPrefix}{bonus:0.#} {unit})</font>";
            }
            else if (shortKey == AttrProtection)
            {
                string unit = Lang.Get("alegacyvsquest:attr-unit-dmg");
                if (unit == "alegacyvsquest:attr-unit-dmg") unit = "ед.";
                formattedValue = $"{prefix}{value:0.#} {unit}";
                bonusFormatted = $"<font color=\"{qualityColor}\">({bonusPrefix}{bonus:0.#} {unit})</font>";
            }
            else if (shortKey == AttrMaxOxygen)
            {
                const float OxygenUnitsPerSecond = 800f;
                float seconds = value / OxygenUnitsPerSecond;
                float bonusSeconds = bonus / OxygenUnitsPerSecond;
                string unit = Lang.Get("alegacyvsquest:attr-unit-sec");
                if (unit == "alegacyvsquest:attr-unit-sec") unit = "сек";
                formattedValue = $"{prefix}{seconds:0.#} {unit}";
                bonusFormatted = $"<font color=\"{qualityColor}\">({bonusPrefix}{bonusSeconds:0.#} {unit})</font>";
            }
            else
            {
                formattedValue = $"{prefix}{value:0.##}";
                bonusFormatted = $"<font color=\"{qualityColor}\">({bonusPrefix}{bonus:0.##})</font>";
            }

            string coloredValue = ColorizeValue(shortKey, value, formattedValue);
            return $"<font color=\"{ColorLabel}\">{displayName}:</font>  {coloredValue} {bonusFormatted}";
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

            // Write charge mode metadata if configured
            if (!string.IsNullOrEmpty(actionItem.chargeMode))
            {
                stack.Attributes.SetString(ChargeMode, actionItem.chargeMode);
            }
            if (actionItem.chargeGatedAttrs != null && actionItem.chargeGatedAttrs.Count > 0)
            {
                stack.Attributes.SetString(ChargeGatedAttrs, JsonConvert.SerializeObject(actionItem.chargeGatedAttrs));
            }
            if (actionItem.chargeMaterials != null && actionItem.chargeMaterials.Count > 0)
            {
                stack.Attributes.SetString(ChargeMaterials, JsonConvert.SerializeObject(actionItem.chargeMaterials));
                stack.Attributes.SetFloat(ChargePerUnit, actionItem.chargePerUnit);
                stack.Attributes.SetFloat(ChargeMaxHours, actionItem.chargeMax);
                if (actionItem.chargeDepletedMult > 0f)
                {
                    stack.Attributes.SetFloat(ChargeDepletedMult, actionItem.chargeDepletedMult);
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
            var treeAttrs = stack.Attributes as TreeAttribute;
            if (treeAttrs != null)
            {
                foreach (var val in treeAttrs)
                {
                    if (val.Key.EndsWith("chargehours"))
                    {
                        float charge = stack.Attributes.GetFloat(val.Key);
                        int rounded = (int)System.Math.Round(charge * 2);
                        hash = hash * 31 + rounded;
                    }
                }
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
