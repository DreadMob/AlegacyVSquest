using ProtoBuf;

namespace VsQuest
{
    /// <summary>
    /// Server → Client: opens the trial shop GUI with current player data.
    /// </summary>
    [ProtoContract]
    public class OpenTrialShopMessage
    {
        [ProtoMember(1)]
        public int Reputation { get; set; }

        [ProtoMember(2)]
        public int VoidShards { get; set; }

        [ProtoMember(3)]
        public string RankName { get; set; }

        [ProtoMember(4)]
        public TrialShopItemData[] ShopItems { get; set; }

        [ProtoMember(5)]
        public int CompletedTrials { get; set; }

        [ProtoMember(6)]
        public int TotalTrials { get; set; }

        /// <summary>Per-boss stats for the stats tab. Key format: "BossName|tier|kills|bestTime|deathless"</summary>
        [ProtoMember(7)]
        public string[] PlayerStats { get; set; }
    }

    /// <summary>
    /// Client → Server: player wants to buy an item from the trial shop.
    /// </summary>
    [ProtoContract]
    public class BuyTrialShopItemMessage
    {
        [ProtoMember(1)]
        public string ItemCode { get; set; }

        [ProtoMember(2)]
        public int Cost { get; set; }
    }

    /// <summary>
    /// Data for a single shop item.
    /// </summary>
    [ProtoContract]
    public class TrialShopItemData
    {
        [ProtoMember(1)]
        public string ItemCode { get; set; }

        [ProtoMember(2)]
        public string NameKey { get; set; }

        [ProtoMember(3)]
        public int Cost { get; set; }

        [ProtoMember(4)]
        public int RequiredReputation { get; set; }

        [ProtoMember(5)]
        public bool IsLocked { get; set; }

        [ProtoMember(6)]
        public int MaxPurchases { get; set; }

        [ProtoMember(7)]
        public int PurchasesMade { get; set; }

        /// <summary>
        /// For case items: localized names of items in the pool (for preview tooltip).
        /// </summary>
        [ProtoMember(8)]
        public string[] CasePoolNames { get; set; }
    }
}
