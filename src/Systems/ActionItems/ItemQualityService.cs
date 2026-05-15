using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    /// <summary>
    /// Service for applying item qualities to action items.
    /// Handles quality chance rolls, bonus calculations, and attribute modifications.
    /// </summary>
    public class ItemQualityService
    {
        private readonly ICoreAPI api;
        private readonly Dictionary<string, ItemQuality> qualityRegistry = new Dictionary<string, ItemQuality>(StringComparer.Ordinal);

        // Cache of qualities by applicable item for fast lookup
        private readonly Dictionary<string, List<ItemQuality>> qualitiesByItem = new Dictionary<string, List<ItemQuality>>(StringComparer.Ordinal);
        private List<ItemQuality> globalQualities = new List<ItemQuality>();

        public ItemQualityService(ICoreAPI api)
        {
            this.api = api;
        }

        /// <summary>
        /// Static registry for access without service instance
        /// </summary>
        public static Dictionary<string, ItemQuality> StaticQualityRegistry { get; private set; } = new Dictionary<string, ItemQuality>(StringComparer.Ordinal);

        /// <summary>
        /// Loads quality configurations from all mods' qualityconfig.json files
        /// </summary>
        public void LoadConfigs()
        {
            if (api == null) return;

            qualityRegistry.Clear();
            qualitiesByItem.Clear();
            globalQualities.Clear();

            foreach (var mod in api.ModLoader.Mods)
            {
                var assets = api.Assets.GetMany<ItemQualityConfig>(api.Logger, "config/qualityconfig", mod.Info.ModID);
                foreach (var asset in assets)
                {
                    if (asset.Value?.qualities == null) continue;

                    foreach (var quality in asset.Value.qualities)
                    {
                        if (string.IsNullOrWhiteSpace(quality.id)) continue;

                        qualityRegistry[quality.id] = quality;
                        StaticQualityRegistry[quality.id] = quality;

                        // Index by applicable items
                        if (quality.applicableItems == null || quality.applicableItems.Count == 0)
                        {
                            globalQualities.Add(quality);
                        }
                        else
                        {
                            foreach (var itemId in quality.applicableItems)
                            {
                                if (!qualitiesByItem.TryGetValue(itemId, out var list))
                                {
                                    list = new List<ItemQuality>();
                                    qualitiesByItem[itemId] = list;
                                }
                                list.Add(quality);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to apply a quality to an action item stack.
        /// Returns true if a quality was applied.
        /// </summary>
        public bool TryApplyQuality(ItemStack stack, ActionItem actionItem, Random rand)
        {
            return TryApplyQuality(stack, actionItem, rand, out _);
        }

        /// <summary>
        /// Tries to apply a quality to the item. If the quality has actionItemOverride,
        /// overrideActionItemId will be set to the replacement action item ID.
        /// </summary>
        public bool TryApplyQuality(ItemStack stack, ActionItem actionItem, Random rand, out string overrideActionItemId)
        {
            overrideActionItemId = null;
            if (stack?.Attributes == null || actionItem == null || rand == null) return false;

            // Check if item already has a quality (exclusive)
            if (stack.Attributes.HasAttribute(ItemAttributeUtils.ItemQualityIdKey))
            {
                return false;
            }

            // Get applicable qualities for this item
            var applicableQualities = GetApplicableQualities(actionItem.id);
            if (applicableQualities.Count == 0) return false;

            // Roll for each quality (first one that succeeds is applied)
            ItemQuality selectedQuality = null;
            foreach (var quality in applicableQualities)
            {
                if (rand.NextDouble() < quality.chance)
                {
                    selectedQuality = quality;
                    break;
                }
            }

            // Fallback: if no quality was rolled, apply the first one (lowest tier)
            if (selectedQuality == null)
            {
                selectedQuality = applicableQualities[0];
            }

            // If quality has an override, signal the caller to replace the item
            if (!string.IsNullOrWhiteSpace(selectedQuality.actionItemOverride))
            {
                overrideActionItemId = selectedQuality.actionItemOverride;
            }

            ApplyQuality(stack, actionItem, selectedQuality, rand);
            return true;
        }

        /// <summary>
        /// Gets all qualities applicable to a specific action item ID
        /// </summary>
        public List<ItemQuality> GetApplicableQualitiesForItem(string actionItemId)
        {
            return GetApplicableQualities(actionItemId);
        }

        private List<ItemQuality> GetApplicableQualities(string actionItemId)
        {
            var result = new List<ItemQuality>();

            // Add global qualities (apply to all items — those with no applicableItems)
            result.AddRange(globalQualities);

            // Add item-specific qualities (only those that explicitly list this item)
            if (!string.IsNullOrWhiteSpace(actionItemId) && qualitiesByItem.TryGetValue(actionItemId, out var specific))
            {
                result.AddRange(specific);
            }

            return result;
        }

        /// <summary>
        /// Gets a quality by ID, or null if not found
        /// </summary>
        public ItemQuality GetQuality(string qualityId)
        {
            if (string.IsNullOrWhiteSpace(qualityId)) return null;
            return qualityRegistry.TryGetValue(qualityId, out var quality) ? quality : null;
        }

        /// <summary>
        /// Gets all registered qualities
        /// </summary>
        public IEnumerable<ItemQuality> GetAllQualities()
        {
            return qualityRegistry.Values;
        }

        /// <summary>
        /// Applies a specific quality to an item stack, modifying attributes
        /// </summary>
        public void ApplyQuality(ItemStack stack, ActionItem actionItem, ItemQuality quality, Random rand)
        {
            // Roll bonus percentage (used when perAttribute is false, or as base for average)
            float baseBonusPercent = (float)rand.NextDouble() * (quality.maxBonusPercent - quality.minBonusPercent) + quality.minBonusPercent;
            float baseBonusMult = baseBonusPercent / 100f;

            // Parse bonus mode
            var bonusMode = ParseBonusMode(quality.bonusMode);

            // Store quality info on stack
            stack.Attributes.SetString(ItemAttributeUtils.ItemQualityIdKey, quality.id);
            stack.Attributes.SetString(ItemAttributeUtils.ItemQualityNameKey, quality.name);
            stack.Attributes.SetString(ItemAttributeUtils.ItemQualityColorKey, quality.color);
            stack.Attributes.SetFloat(ItemAttributeUtils.ItemQualityBonusPercentKey, baseBonusPercent);

            // Calculate and apply bonuses
            var bonusData = new Dictionary<string, float>();
            float totalBonusPercent = 0f;
            int appliedCount = 0;

            if (actionItem.attributes != null)
            {
                foreach (var attr in actionItem.attributes)
                {
                    float originalValue = attr.Value;
                    bool isBuff = ItemAttributeUtils.IsValueBeneficial(attr.Key, originalValue);
                    bool isDebuff = !isBuff && originalValue != 0;

                    bool shouldApply = bonusMode == ItemQualityBonusMode.All ||
                        (bonusMode == ItemQualityBonusMode.BuffsOnly && isBuff) ||
                        (bonusMode == ItemQualityBonusMode.DebuffsOnly && isDebuff);

                    if (shouldApply && originalValue != 0)
                    {
                        // Skip attributes that should not be scaled by quality (but may be overridden below)
                        if (ItemAttributeUtils.IsQualityExemptAttribute(attr.Key))
                        {
                            continue;
                        }
                        // Roll individual bonus if perAttribute is enabled
                        float bonusMult = quality.perAttribute
                            ? ((float)rand.NextDouble() * (quality.maxBonusPercent - quality.minBonusPercent) + quality.minBonusPercent) / 100f
                            : baseBonusMult;

                        if (quality.perAttribute)
                        {
                            totalBonusPercent += bonusMult * 100f;
                            appliedCount++;
                        }

                        float bonus;
                        float newValue;

                        if (isBuff)
                        {
                            // Buffs: make the value more beneficial
                            // For normal attrs (positive=good): increase
                            // For inverted attrs (negative=good, e.g. hungerrate -0.1): make more negative
                            bonus = Math.Abs(originalValue) * bonusMult;
                            newValue = originalValue > 0 ? originalValue + bonus : originalValue - bonus;
                        }
                        else
                        {
                            // Debuffs: make the value less harmful
                            // For normal attrs (negative=bad): reduce magnitude
                            // For inverted attrs (positive=bad, e.g. hungerrate 0.3): reduce magnitude
                            bonus = Math.Abs(originalValue) * bonusMult;
                            newValue = originalValue > 0 ? originalValue - bonus : originalValue + bonus;
                        }

                        // Store the bonus amount for tooltip display
                        bonusData[attr.Key] = isBuff ? bonus : -bonus;

                        // Update the attribute on the stack
                        stack.Attributes.SetFloat(ItemAttributeUtils.GetKey(attr.Key), newValue);
                    }
                }
            }

            // If perAttribute, store the average bonus percent for display
            if (quality.perAttribute && appliedCount > 0)
            {
                float avgBonusPercent = totalBonusPercent / appliedCount;
                stack.Attributes.SetFloat(ItemAttributeUtils.ItemQualityBonusPercentKey, avgBonusPercent);
                baseBonusPercent = avgBonusPercent; // use average for tier calculation
            }

            // Calculate and store quality tier (I-V) based on final average bonus within min-max range
            int qualityTier = CalculateQualityTier(baseBonusPercent, quality.minBonusPercent, quality.maxBonusPercent);
            stack.Attributes.SetInt("alegacyvsquest:qualityTier", qualityTier);

            // Special handling: damagetier upgrades at higher quality tiers (45%+ bonus = tier +1)
            if (actionItem.attributes != null && actionItem.attributes.ContainsKey(ItemAttributeUtils.AttrDamageTier))
            {
                float baseTier = actionItem.attributes[ItemAttributeUtils.AttrDamageTier];
                if (baseBonusPercent >= 35f)
                {
                    float newTier = baseTier + 1f;
                    stack.Attributes.SetFloat(ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrDamageTier), newTier);
                    bonusData[ItemAttributeUtils.AttrDamageTier] = 1f;
                }
                else
                {
                    stack.Attributes.SetFloat(ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrDamageTier), baseTier);
                }
            }

            // Store bonus data as JSON for tooltip
            if (bonusData.Count > 0)
            {
                stack.Attributes.SetString(ItemAttributeUtils.ItemQualityBonusDataKey, JsonConvert.SerializeObject(bonusData));
            }
        }

        private ItemQualityBonusMode ParseBonusMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return ItemQualityBonusMode.All;

            return mode.ToLowerInvariant() switch
            {
                "buffs" => ItemQualityBonusMode.BuffsOnly,
                "debuffs" => ItemQualityBonusMode.DebuffsOnly,
                _ => ItemQualityBonusMode.All
            };
        }

        /// <summary>
        /// Checks if an item stack has a quality applied
        /// </summary>
        public static bool HasQuality(ItemStack stack)
        {
            return stack?.Attributes != null && stack.Attributes.HasAttribute(ItemAttributeUtils.ItemQualityIdKey);
        }

        /// <summary>
        /// Gets the quality name for an item stack
        /// </summary>
        public static string GetQualityName(ItemStack stack)
        {
            if (stack?.Attributes == null) return null;
            return stack.Attributes.GetString(ItemAttributeUtils.ItemQualityNameKey);
        }

        /// <summary>
        /// Gets the quality color for an item stack
        /// </summary>
        public static string GetQualityColor(ItemStack stack)
        {
            if (stack?.Attributes == null) return "#FFFFFF";
            return stack.Attributes.GetString(ItemAttributeUtils.ItemQualityColorKey, "#FFFFFF");
        }

        /// <summary>
        /// Gets the bonus data dictionary for an item stack
        /// </summary>
        public static Dictionary<string, float> GetBonusData(ItemStack stack)
        {
            if (stack?.Attributes == null) return null;

            var json = stack.Attributes.GetString(ItemAttributeUtils.ItemQualityBonusDataKey);
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, float>>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Calculate quality tier (1-5) based on where the bonus falls within the min-max range.
        /// Tier I = bottom 20%, Tier V = top 20%.
        /// </summary>
        public static int CalculateQualityTier(float bonusPercent, float minBonus, float maxBonus)
        {
            if (maxBonus <= minBonus) return 1;

            float normalized = (bonusPercent - minBonus) / (maxBonus - minBonus); // 0.0 to 1.0
            normalized = Math.Clamp(normalized, 0f, 1f);

            // 5 tiers: 0-0.2 = I, 0.2-0.4 = II, 0.4-0.6 = III, 0.6-0.8 = IV, 0.8-1.0 = V
            int tier = (int)(normalized * 5f) + 1;
            return Math.Min(tier, 5);
        }

        /// <summary>
        /// Get roman numeral string for a tier (1-5).
        /// </summary>
        public static string GetTierRoman(int tier)
        {
            return tier switch
            {
                1 => "I",
                2 => "II",
                3 => "III",
                4 => "IV",
                5 => "V",
                _ => "I"
            };
        }
    }
}
