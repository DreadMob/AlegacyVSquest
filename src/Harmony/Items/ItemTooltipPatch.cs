using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Client;
using Vintagestory.GameContent;

namespace VsQuest.Harmony
{
    public static class ItemTooltipPatcher
    {
        // Tooltip cache to avoid recomputing every frame on hover
        private static readonly Dictionary<int, CachedTooltip> TooltipCache = new Dictionary<int, CachedTooltip>();
        private const int MaxCacheSize = 128;

        public static void ClearCache()
        {
            TooltipCache.Clear();
        }

        private class CachedTooltip
        {
            public string Tooltip;
            public int StackFingerprint;
            public long Timestamp;
        }

        private static int GetStackFingerprint(ItemStack stack)
        {
            if (stack?.Attributes == null) return 0;
            // Fast fingerprint based on item code + attributes hash + durability + condition
            int hash = stack.Collectible?.Code?.GetHashCode() ?? 0;
            hash = hash * 31 + stack.Attributes.GetHashCode();
            // Include durability for tools
            hash = hash * 31 + stack.Attributes.GetInt("durability", 0);
            // Include condition for wearables (clothing)
            hash = hash * 31 + (int)(stack.Attributes.GetFloat("condition", 1f) * 1000);
            // Include quality for proper cache invalidation
            string qualityId = stack.Attributes.GetString(ItemAttributeUtils.ItemQualityIdKey);
            if (!string.IsNullOrEmpty(qualityId))
            {
                hash = hash * 31 + qualityId.GetHashCode();
                // Include bonus percent to differentiate quality rolls
                hash = hash * 31 + (int)(stack.Attributes.GetFloat(ItemAttributeUtils.ItemQualityBonusPercentKey, 0f) * 100);
            }
            return hash;
        }

        private static string GetCachedTooltip(ItemSlot inSlot, string inputTooltip)
        {
            if (inSlot?.Itemstack == null) return null;
            int fp = GetStackFingerprint(inSlot.Itemstack);
            if (fp == 0) return null;

            long now = DateTime.UtcNow.Ticks;
            if (TooltipCache.TryGetValue(fp, out var cached) && cached.StackFingerprint == fp)
            {
                // Cache valid if same stack fingerprint
                return cached.Tooltip;
            }
            return null;
        }

        private static void SetCachedTooltip(ItemSlot inSlot, string result)
        {
            if (inSlot?.Itemstack == null) return;
            int fp = GetStackFingerprint(inSlot.Itemstack);
            if (fp == 0) return;

            // Prevent cache bloat
            if (TooltipCache.Count >= MaxCacheSize)
            {
                TooltipCache.Clear();
            }

            TooltipCache[fp] = new CachedTooltip
            {
                Tooltip = result,
                StackFingerprint = fp,
                Timestamp = DateTime.UtcNow.Ticks
            };
        }
        private static void TrimEndNewlines(StringBuilder sb)
        {
            if (sb == null) return;

            while (sb.Length > 0)
            {
                char c = sb[sb.Length - 1];
                if (c == '\n' || c == '\r') sb.Length--;
                else break;
            }
        }


        public static void ModifyTooltip(ItemSlot inSlot, StringBuilder dsc)
        {
            if (inSlot?.Itemstack?.Attributes == null) return;

            string actionsJson = inSlot.Itemstack.Attributes.GetString("alegacyvsquest:actions");
            if (string.IsNullOrEmpty(actionsJson)) return;

            // Check cache first
            string originalTooltip = dsc.ToString();
            string cached = GetCachedTooltip(inSlot, originalTooltip);
            if (cached != null)
            {
                dsc.Clear();
                dsc.Append(cached);
                return;
            }

            ITreeAttribute attrs = inSlot.Itemstack.Attributes;

            HashSet<string> hideVanilla = new HashSet<string>();
            string hideVanillaJson = attrs.GetString("alegacyvsquest:hideVanilla");
            if (!string.IsNullOrEmpty(hideVanillaJson))
            {
                try { hideVanilla = new HashSet<string>(JsonConvert.DeserializeObject<List<string>>(hideVanillaJson)); } catch (Exception) { /* Swallow - invalid JSON, use default */ }
            }

            // Always hide mod source on action items
            hideVanilla.Add("modsource");

            string customDesc = attrs.GetString(ItemAttributeUtils.QuestDescKey);
            bool hasCustomDesc = !string.IsNullOrEmpty(customDesc);
            bool hideDesc = hasCustomDesc || hideVanilla.Contains("description");

            string currentTooltip = dsc.ToString();

            // Check for item quality
            string qualityId = attrs.GetString(ItemAttributeUtils.ItemQualityIdKey);
            string qualityName = attrs.GetString(ItemAttributeUtils.ItemQualityNameKey);
            string qualityColor = attrs.GetString(ItemAttributeUtils.ItemQualityColorKey, "#FFFFFF");
            Dictionary<string, float> qualityBonusData = ItemQualityService.GetBonusData(inSlot.Itemstack);
            bool hasQuality = !string.IsNullOrEmpty(qualityId) && !string.IsNullOrEmpty(qualityName);
            float qualityBonusPercent = attrs.GetFloat(ItemAttributeUtils.ItemQualityBonusPercentKey, 0f);
            // Always show quality header if quality is set
            bool showQualityHeader = hasQuality;

            dsc.Clear();

            // Show quality header at the top
            if (showQualityHeader)
            {
                dsc.AppendLine($"<font color=\"{qualityColor}\">\u25AC\u25AC\u25AC \u2666\u2666 {qualityName} \u2666\u2666 \u25AC\u25AC\u25AC</font>");
            }

            // Show condition at the top for wearable action items
            if (inSlot.Itemstack?.Collectible.GetCollectibleInterface<Vintagestory.API.Common.IWearable>() != null)
            {
                float conditionTop = attrs.GetFloat("condition", 1f);
                string condStrTop = (((double)conditionTop > 0.5) ? Lang.Get("clothingcondition-good", (int)(conditionTop * 100f)) : (((double)conditionTop > 0.4) ? Lang.Get("clothingcondition-worn", (int)(conditionTop * 100f)) : (((double)conditionTop > 0.3) ? Lang.Get("clothingcondition-heavilyworn", (int)(conditionTop * 100f)) : (((double)conditionTop > 0.2) ? Lang.Get("clothingcondition-tattered", (int)(conditionTop * 100f)) : ((!((double)conditionTop > 0.1)) ? Lang.Get("clothingcondition-terrible", (int)(conditionTop * 100f)) : Lang.Get("clothingcondition-heavilytattered", (int)(conditionTop * 100f)))))));
                string colorTop = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99f, conditionTop * 200f)]);
                dsc.AppendLine(Lang.Get("Condition:") + " <font color=\"" + colorTop + "\">" + condStrTop + "</font>");
            }

            if (hasCustomDesc && currentTooltip.Contains(customDesc))
            {
                currentTooltip = currentTooltip.Replace(customDesc, "");
            }

            // Description will be added at the end — don't add it here

            string[] lines = currentTooltip.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            bool lastLineWasEmpty = true;
            bool startedSkippingLeadingDesc = false;
            bool skippedLeadingDescBlock = !hideDesc;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                bool isLineEmpty = string.IsNullOrWhiteSpace(trimmed);

                if (trimmed.StartsWith("Максимальное тепло:", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("Maximum warmth:", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("Max warmth:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // If the action item provides its own description, drop the leading vanilla description block
                // (first paragraph).
                if (!skippedLeadingDescBlock)
                {
                    if (!startedSkippingLeadingDesc)
                    {
                        if (isLineEmpty) continue;
                        startedSkippingLeadingDesc = true;
                        continue;
                    }
                    else
                    {
                        // If we reach an empty line, we're done skipping the desc block
                        if (isLineEmpty)
                        {
                            skippedLeadingDescBlock = true;
                            continue;
                        }
                        // Still skipping vanilla description.
                        continue;
                    }
                }

                if (isLineEmpty)
                {
                    if (!lastLineWasEmpty)
                    {
                        dsc.AppendLine();
                        lastLineWasEmpty = true;
                    }
                    continue;
                }

                bool shouldHide = false;

                if (hideVanilla.Contains("durability"))
                {
                    // Hide durability lines (tools)
                    if (trimmed.StartsWith("Durability:") || trimmed.StartsWith(Lang.Get("Durability:")) ||
                        trimmed.StartsWith("Прочность:"))
                    {
                        shouldHide = true;
                    }
                }

                if (!shouldHide && hideVanilla.Contains("condition"))
                {
                    // Hide condition lines (wearables)
                    if (trimmed.StartsWith("Condition:") || trimmed.StartsWith(Lang.Get("Condition:")) ||
                        trimmed.StartsWith("Состояние:"))
                    {
                        shouldHide = true;
                    }
                }

                if (!shouldHide && hideVanilla.Contains("miningspeed"))
                {
                    if (trimmed.StartsWith("Tool Tier:") || trimmed.StartsWith(Lang.Get("Tool Tier: {0}"))) shouldHide = true;
                    else if (trimmed.Contains("mining speed") || trimmed.Contains(Lang.Get("item-tooltip-miningspeed"))) shouldHide = true;
                }

                if (!shouldHide && hideVanilla.Contains("attackpower"))
                {
                    if (trimmed.StartsWith("Attack power:") || trimmed.StartsWith("Attack tier:")) shouldHide = true;
                    else if (trimmed.Contains("Attack power:") || trimmed.Contains("Attack tier:")) shouldHide = true;
                    else if (trimmed.Contains("Уровень атаки:") || trimmed.Contains("Сила атаки:")) shouldHide = true;
                    else if (trimmed.Contains("урона")) shouldHide = true;
                    else if (trimmed.Contains("damage") && !trimmed.Contains("<font")) shouldHide = true;
                    else if (trimmed.Contains("точности") || trimmed.Contains("accuracy")) shouldHide = true;
                    else if (trimmed.StartsWith("+") && trimmed.Contains("%") && trimmed.Length < 30) shouldHide = true;
                }

                if (!shouldHide && (hideVanilla.Contains("protection") || hideVanilla.Contains("armor")))
                {
                    if (trimmed.StartsWith("Flat damage reduction:") || trimmed.StartsWith("Percent protection:") || trimmed.StartsWith("Protection tier:")) shouldHide = true;
                    else if (trimmed.Contains("Protection from rain") || trimmed.StartsWith("High damage tier resistant")) shouldHide = true;
                }

                if (!shouldHide && hideVanilla.Contains("warmth"))
                {
                    // Hide warmth lines but not condition lines
                    if (trimmed.Contains("°C") && (trimmed.Contains("+") || trimmed.Contains("Warmth") || trimmed.Contains("тепло")))
                    {
                        // Don't hide if it's a condition line (vanilla shows condition + warmth together)
                        if (!trimmed.StartsWith("Condition:") && !trimmed.StartsWith(Lang.Get("Condition:")) && !trimmed.StartsWith("Состояние:"))
                        {
                            shouldHide = true;
                        }
                    }
                }

                if (!shouldHide && hideVanilla.Contains("temperature"))
                {
                    if (trimmed.StartsWith("Temperature:")) shouldHide = true;
                }

                if (!shouldHide && hideVanilla.Contains("nutrition"))
                {
                    if (trimmed.StartsWith("Satiety:") || trimmed.StartsWith("Nutrients:") || trimmed.StartsWith("Food Category:")) shouldHide = true;
                    else if (trimmed.Contains("sat") && (trimmed.Contains("veg") || trimmed.Contains("fruit") || trimmed.Contains("grain") || trimmed.Contains("prot") || trimmed.Contains("dairy"))) shouldHide = true;
                }

                if (!shouldHide && hideVanilla.Contains("storage"))
                {
                    if (trimmed.StartsWith("Slots:") || trimmed.StartsWith("Storage Slots:")) shouldHide = true;
                    else if (trimmed.StartsWith("Containable:")) shouldHide = true;
                }

                if (!shouldHide && hideVanilla.Contains("combustible"))
                {
                    if (trimmed.StartsWith("Burn temperature:") || trimmed.StartsWith("Burn duration:")) shouldHide = true;
                }

                if (!shouldHide && (hideVanilla.Contains("grinding") || hideVanilla.Contains("crushing")))
                {
                    if (trimmed.StartsWith("Grinds into") || trimmed.StartsWith("Crushes into")) shouldHide = true;
                }

                if (!shouldHide && hideVanilla.Contains("modsource"))
                {
                    if (trimmed.StartsWith("Mod:") || trimmed.StartsWith("Мод:")) shouldHide = true;
                }

                if (!shouldHide && hideVanilla.Contains("walkspeed"))
                {
                    if (trimmed.StartsWith("Walk speed:")) shouldHide = true;
                }

                if (!shouldHide)
                {
                    dsc.AppendLine(line);
                    lastLineWasEmpty = false;
                }
            }

            HashSet<string> showAttrs = new HashSet<string>();
            string showAttrsJson = attrs.GetString("alegacyvsquest:showAttrs");
            if (!string.IsNullOrEmpty(showAttrsJson))
            {
                try { showAttrs = new HashSet<string>(JsonConvert.DeserializeObject<List<string>>(showAttrsJson)); } catch (Exception) { /* Swallow - invalid JSON, use default */ }
            }

            string currentDsc = dsc.ToString();

            // Detect if this item has a charge system (any *chargehours attribute)
            bool hasChargeAttr = false;
            foreach (var kvp in attrs)
            {
                if (kvp.Key.EndsWith("chargehours"))
                {
                    hasChargeAttr = true;
                    break;
                }
            }

            // Collect attributes into 3 groups: special, buffs, debuffs
            var specialLines = new List<string>();
            var buffLines = new List<string>();
            var debuffLines = new List<string>();

            foreach (var kvp in attrs)
            {
                if (kvp.Key.StartsWith(ItemAttributeUtils.AttrPrefix))
                {
                    string shortKey = kvp.Key.Substring(ItemAttributeUtils.AttrPrefix.Length);
                    if (!showAttrs.Contains(shortKey)) continue;

                    float value;
                    if (shortKey == ItemAttributeUtils.AttrSecondChanceCharges)
                    {
                        value = ItemAttributeUtils.GetAttributeFloat(inSlot.Itemstack, shortKey, 0f);
                    }
                    else
                    {
                        value = ItemAttributeUtils.GetAttributeFloatScaled(inSlot.Itemstack, shortKey, 0f);
                    }
                    bool showZero = ItemAttributeUtils.IsSpecialAttribute(shortKey);
                    if (!showZero && hasChargeAttr)
                    {
                        showZero = true;
                    }
                    if (value == 0f && !showZero) continue;

                    // Check if this attribute has a quality bonus
                    float bonusValue = 0f;
                    bool hasBonus = hasQuality && qualityBonusData != null && qualityBonusData.TryGetValue(shortKey, out bonusValue);

                    string lineToAdd;
                    if (hasBonus && bonusValue != 0f)
                    {
                        lineToAdd = ItemAttributeUtils.FormatAttributeWithBonus(kvp.Key, value, bonusValue, qualityColor);
                    }
                    else
                    {
                        lineToAdd = ItemAttributeUtils.FormatAttributeForTooltip(kvp.Key, value);
                    }

                    if (currentDsc.Contains(lineToAdd)) continue;

                    if (ItemAttributeUtils.IsSpecialAttribute(shortKey))
                    {
                        specialLines.Add(lineToAdd);
                    }
                    else if (ItemAttributeUtils.IsValueBeneficial(shortKey, value))
                    {
                        buffLines.Add(lineToAdd);
                    }
                    else
                    {
                        debuffLines.Add(lineToAdd);
                    }
                }
            }

            // Write sorted groups with section headers
            bool hasAnyAttrs = specialLines.Count > 0 || buffLines.Count > 0 || debuffLines.Count > 0;
            if (hasAnyAttrs)
            {
                TrimEndNewlines(dsc);

                // Special (charges, timers) — just listed directly, no header, no extra newline
                if (specialLines.Count > 0)
                {
                    if (dsc.Length > 0) dsc.AppendLine();
                    foreach (var line in specialLines) dsc.AppendLine(ItemAttributeUtils.PrefixSpecial + line);
                }

                // Buffs — green with section header
                if (buffLines.Count > 0)
                {
                    if (dsc.Length > 0) dsc.AppendLine();
                    dsc.AppendLine(ItemAttributeUtils.SectionBuffs);
                    foreach (var line in buffLines) dsc.AppendLine(ItemAttributeUtils.PrefixBuff + line);
                }

                // Debuffs — red with section header
                if (debuffLines.Count > 0)
                {
                    if (dsc.Length > 0) dsc.AppendLine();
                    dsc.AppendLine(ItemAttributeUtils.SectionDebuffs);
                    foreach (var line in debuffLines) dsc.AppendLine(ItemAttributeUtils.PrefixDebuff + line);
                }
            }

            // Show durability for action items if not already shown
            if (attrs.HasAttribute("durability"))
            {
                int durability = attrs.GetInt("durability", 0);
                int maxDurability = inSlot.Itemstack.Collectible.GetMaxDurability(inSlot.Itemstack);
                if (maxDurability > 1)
                {
                    string durLine = Lang.Get("Durability: {0} / {1}", durability, maxDurability);
                    currentDsc = dsc.ToString();
                    if (!currentDsc.Contains(durLine) && !currentDsc.Contains("Durability:") && !currentDsc.Contains("Прочность:"))
                    {
                        TrimEndNewlines(dsc);
                        if (dsc.Length > 0) dsc.AppendLine();
                        dsc.AppendLine(durLine);
                    }
                }
            }

            // Description block at the very end (only show separator if there were stats above)
            if (hasCustomDesc)
            {
                TrimEndNewlines(dsc);
                if (dsc.Length > 0 && hasAnyAttrs)
                {
                    dsc.AppendLine();
                    dsc.AppendLine(ItemAttributeUtils.SectionDesc);
                }
                else if (dsc.Length > 0)
                {
                    dsc.AppendLine();
                }
                dsc.AppendLine(customDesc);
            }

            // Cache the final tooltip result
            TrimEndNewlines(dsc);
            string result = dsc.ToString();
            SetCachedTooltip(inSlot, result);
        }
    }

    [HarmonyPatch(typeof(CollectibleObject), "GetHeldItemInfo")]
    public class CollectibleObject_GetHeldItemInfo_Patch
    {
        public static void Postfix(ItemSlot inSlot, StringBuilder dsc)
        {
            if (!HarmonyPatchSwitches.ItemTooltipEnabled(HarmonyPatchSwitches.ItemTooltip_CollectibleObject_GetHeldItemInfo)) return;
            ItemTooltipPatcher.ModifyTooltip(inSlot, dsc);
        }
    }

    // ItemWearable is obsolete in 1.22, GetHeldItemInfo is inherited from Item
    // The CollectibleObject patch handles all items including wearables
}
