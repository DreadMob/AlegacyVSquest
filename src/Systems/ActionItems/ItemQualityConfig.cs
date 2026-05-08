using System.Collections.Generic;

namespace VsQuest
{
    /// <summary>
    /// Configuration for item qualities loaded from qualityconfig.json
    /// </summary>
    public class ItemQualityConfig
    {
        public List<ItemQuality> qualities { get; set; } = new List<ItemQuality>();
    }

    /// <summary>
    /// Defines a quality that can be applied to action items.
    /// </summary>
    public class ItemQuality
    {
        /// <summary>
        /// Unique identifier for this quality (e.g., "prestigious")
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Display name shown in tooltip (e.g., "ПРЕСТИЖНОЕ")
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// Hex color for the quality header in tooltip (e.g., "#FFD700")
        /// </summary>
        public string color { get; set; } = "#FFFFFF";

        /// <summary>
        /// Chance for this quality to be applied (0.0 to 1.0)
        /// </summary>
        public float chance { get; set; } = 0.1f;

        /// <summary>
        /// Minimum bonus percentage (e.g., 5 for 5%)
        /// </summary>
        public float minBonusPercent { get; set; } = 5f;

        /// <summary>
        /// Maximum bonus percentage (e.g., 25 for 25%)
        /// </summary>
        public float maxBonusPercent { get; set; } = 25f;

        /// <summary>
        /// Which attributes to apply bonus to:
        /// - "all": Apply to all attributes (buffs increased, debuffs reduced)
        /// - "buffs": Only apply to positive values (buffs)
        /// - "debuffs": Only apply to negative values (debuffs reduced)
        /// </summary>
        public string bonusMode { get; set; } = "all";

        /// <summary>
        /// If true, each attribute gets its own random bonus percentage.
        /// If false, one percentage is rolled and applied to all attributes.
        /// </summary>
        public bool perAttribute { get; set; } = false;

        /// <summary>
        /// If true, only one quality can be applied to an item.
        /// If false, multiple qualities could stack (not recommended).
        /// </summary>
        public bool exclusive { get; set; } = true;

        /// <summary>
        /// List of action item IDs that this quality can be applied to.
        /// If empty, applies to all action items.
        /// </summary>
        public List<string> applicableItems { get; set; } = new List<string>();
    }

    /// <summary>
    /// Bonus mode enum for item quality
    /// </summary>
    public enum ItemQualityBonusMode
    {
        All,
        BuffsOnly,
        DebuffsOnly
    }
}
