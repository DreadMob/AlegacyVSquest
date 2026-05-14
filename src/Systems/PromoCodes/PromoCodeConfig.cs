using System;
using System.Collections.Generic;

namespace VsQuest
{
    /// <summary>
    /// Root configuration for the promo code system.
    /// Loaded from assets: config/promocodes.json
    /// Runtime codes (created via commands) are stored in mod config: promocodes-runtime.json
    /// </summary>
    public class PromoCodeConfig
    {
        public List<PromoCode> codes { get; set; } = new List<PromoCode>();
        public PromoCodeSettings settings { get; set; } = new PromoCodeSettings();
    }

    /// <summary>
    /// Global settings for the promo code system.
    /// </summary>
    public class PromoCodeSettings
    {
        /// <summary>Max failed attempts per minute before temporary lockout.</summary>
        public int maxAttemptsPerMinute { get; set; } = 5;

        /// <summary>Minutes to lock a player out after exceeding lockout threshold.</summary>
        public int lockoutMinutes { get; set; } = 10;

        /// <summary>Total failed attempts before lockout kicks in.</summary>
        public int lockoutThreshold { get; set; } = 15;

        /// <summary>If true, codes are case-insensitive.</summary>
        public bool caseInsensitive { get; set; } = true;
    }

    /// <summary>
    /// A single promo code definition.
    /// </summary>
    public class PromoCode
    {
        /// <summary>The code string players must enter.</summary>
        public string code { get; set; }

        /// <summary>
        /// Type of code:
        /// "single" — one use total on the server (first come first served).
        /// "personal" — one use per player.
        /// "multi" — limited total uses (see maxUses).
        /// "unlimited" — no limits.
        /// </summary>
        public string type { get; set; } = "personal";

        /// <summary>Max total uses for "multi" type. 0 = unlimited.</summary>
        public int maxUses { get; set; } = 0;

        /// <summary>Rewards given when the code is redeemed.</summary>
        public List<PromoCodeReward> rewards { get; set; } = new List<PromoCodeReward>();

        /// <summary>Conditions that must be met to redeem.</summary>
        public PromoCodeConditions conditions { get; set; } = new PromoCodeConditions();

        /// <summary>Lang key for success message shown to the player. If empty, uses default.</summary>
        public string message { get; set; }

        /// <summary>Whether this code is currently active.</summary>
        public bool enabled { get; set; } = true;
    }

    /// <summary>
    /// A reward entry for a promo code.
    /// </summary>
    public class PromoCodeReward
    {
        /// <summary>
        /// Reward type:
        /// "actionItem" — give an action item by ID from itemconfig.json
        /// "item" — give a vanilla/modded item by asset code
        /// "quest" — start a quest by ID
        /// "reputation" — add reputation
        /// </summary>
        public string type { get; set; }

        /// <summary>For "actionItem": the action item ID. For "item": the item/block code (e.g. "game:bread-spelt").</summary>
        public string itemId { get; set; }

        /// <summary>For "item": alternative field name for item code.</summary>
        public string itemCode { get; set; }

        /// <summary>Amount of items to give. Default 1.</summary>
        public int amount { get; set; } = 1;

        /// <summary>For "actionItem": whether to apply quality roll.</summary>
        public bool applyQuality { get; set; } = false;

        /// <summary>For "actionItem": force a specific quality tier (e.g. "legendary"). Null = random roll.</summary>
        public string forceQuality { get; set; }

        /// <summary>For "quest": the quest ID to start.</summary>
        public string questId { get; set; }

        /// <summary>For "reputation": the NPC or faction ID.</summary>
        public string reputationId { get; set; }

        /// <summary>For "reputation": amount to add.</summary>
        public int reputationAmount { get; set; } = 0;

        /// <summary>For "reputation": whether it's a faction ("faction") or npc ("npc") reputation.</summary>
        public string reputationType { get; set; } = "npc";
    }

    /// <summary>
    /// Conditions for promo code redemption.
    /// </summary>
    public class PromoCodeConditions
    {
        /// <summary>Code is only valid after this date (UTC). Null = no start restriction.</summary>
        public string validFrom { get; set; }

        /// <summary>Code is only valid until this date (UTC). Null = no end restriction.</summary>
        public string validUntil { get; set; }

        /// <summary>Player must have completed these quests to redeem.</summary>
        public List<string> requiredQuests { get; set; } = new List<string>();
    }
}
