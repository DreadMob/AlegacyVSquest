using System.Collections.Generic;

namespace VsQuest
{
    /// <summary>
    /// Top-level config for Trial Warden NPC, loaded from config/npcs/trialwarden.json.
    /// Contains shop, ranks, dialogues data.
    /// </summary>
    public class TrialNpcConfig
    {
        public string npcId;
        public string nameKey;
        public string titleKey;
        public bool questGiver;
        public bool trialActiveOnly;
        public int trialMaxTier = 3;
        public TrialShopConfigBlock shop;
    }

    /// <summary>
    /// Shop configuration block.
    /// </summary>
    public class TrialShopConfigBlock
    {
        public string currencyKey;
        public List<TrialShopConfigItem> items = new();
    }

    /// <summary>
    /// One purchasable shop item.
    /// </summary>
    public class TrialShopConfigItem
    {
        /// <summary>
        /// Item code to give. For trial cases, can be "case:tier1" / "case:tier2" / "case:tier3"
        /// (virtual identifier — opens a quality roll instead of giving an item).
        /// </summary>
        public string itemCode;

        /// <summary>
        /// Localization key for display name. Optional, falls back to item name.
        /// </summary>
        public string nameKey;

        /// <summary>
        /// Cost in Void Shards.
        /// </summary>
        public int cost;

        /// <summary>
        /// Required reputation to unlock display. 0 = always visible.
        /// </summary>
        public int requiredReputation;

        /// <summary>
        /// Max purchases per player. 0 or -1 = unlimited.
        /// </summary>
        public int maxPurchases;

        /// <summary>
        /// For virtual case items: tier of the case (1, 2, or 3).
        /// </summary>
        public int caseTier;

        /// <summary>
        /// For virtual case items: pool of base items (without quality suffix).
        /// </summary>
        public string[] casePool;

        /// <summary>
        /// For fixed-quality shop items: quality tier to apply (1=Dim, 2=Shimmering, 3=Radiant, 4=Abyssal).
        /// 0 = no quality (plain item).
        /// </summary>
        public int fixedQuality;
    }
}
