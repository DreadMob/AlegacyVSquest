
using System.Collections.Generic;

namespace VsQuest
{
    public class ItemConfig
    {
        public List<ActionItem> actionItems { get; set; } = new List<ActionItem>();
    }

    public class ActionItem
    {
        public string id { get; set; }
        public string itemCode { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public List<ItemAction> actions { get; set; } = new List<ItemAction>();
        public List<ActionItemMode> modes { get; set; } = new List<ActionItemMode>();
        public string sourceQuestId { get; set; }
        public bool triggerOnInventoryAdd { get; set; } = false;
        // blockMove: restrict movement (hotbar-only). blockEquip: forbid equipping into character slots.
        // blockDrop: forbid manual dropping. blockDeath: forbid dropping on death.
        public bool blockMove { get; set; } = false;
        public bool blockEquip { get; set; } = false;
        public bool blockDrop { get; set; } = false;
        public bool blockDeath { get; set; } = false;
        public bool blockGroundStorage { get; set; } = false;
        public Dictionary<string, float> attributes { get; set; } = new Dictionary<string, float>();

        /// Charge system configuration.
        /// "chargeMode": "all" (all stats gated by charge) or "partial" (only chargeGatedAttrs gated).
        /// If omitted, defaults to "all" when a *chargehours attribute is present.
        public string chargeMode { get; set; }
        /// List of attribute short keys that are gated by charge (only used when chargeMode = "partial").
        public List<string> chargeGatedAttrs { get; set; }
        /// List of item code substrings that can be used to charge this item (e.g. ["phosphorite", "fat", "hide-"]).
        /// The source item's code path is checked with Contains() against each entry.
        public List<string> chargeMaterials { get; set; }
        /// How many hours of charge each unit of material adds. Default: 8.
        public float chargePerUnit { get; set; } = 8f;
        /// Maximum charge in hours. Default: 100.
        public float chargeMax { get; set; } = 100f;
        /// Multiplier applied to gated attributes when charge is fully depleted. Default: 0 (no effect).
        /// E.g. 0.267 means gated attrs give 26.7% of their value even without charge.
        public float chargeDepletedMult { get; set; } = 0f;

        /// List of custom attribute keys to show in tooltip (e.g., ["attackpower", "warmth"])
        /// If empty, NO custom attributes are shown.
        public List<string> showAttributes { get; set; } = new List<string>();

        /// List of vanilla tooltip sections to hide/suppress.
        /// Available values:
        /// - "durability" : Hides "Durability: X / Y"
        /// - "miningspeed" : Hides tool tier and mining speeds
        /// - "storage" : Hides bag storage slots and contents
        /// - "nutrition" : Hides food satiety and nutrition info
        /// - "attackpower" : Hides vanilla attack power and tier
        /// - "combustible" : Hides burn temperature, smelting info
        /// - "grinding" : Hides grinding output
        /// - "crushing" : Hides pulverizer output
        /// - "temperature" : Hides current item temperature
        /// - "modsource" : Hides which mod the item is from
        /// If empty, ALL vanilla tooltips are shown (re-implemented logic).
        public List<string> hideVanillaTooltips { get; set; } = new List<string>();
    }

    public class ActionItemMode
    {
        public string id { get; set; }
        public string name { get; set; }
        public string icon { get; set; }
        public List<ItemAction> actions { get; set; } = new List<ItemAction>();
    }

    public class ItemAction
    {
        public string id { get; set; }
        public string[] args { get; set; }
    }
}
