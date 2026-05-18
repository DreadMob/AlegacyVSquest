using ProtoBuf;

namespace VsQuest
{
    /// <summary>
    /// Server → Client: starts the case opening animation.
    /// </summary>
    [ProtoContract]
    public class StartCaseOpenAnimationMessage
    {
        /// <summary>All possible item IDs in the pool (for spinning display).</summary>
        [ProtoMember(1)]
        public string[] PoolItemIds { get; set; }

        /// <summary>Display names for pool items (parallel array).</summary>
        [ProtoMember(2)]
        public string[] PoolItemNames { get; set; }

        /// <summary>Base item codes for rendering icons (parallel array).</summary>
        [ProtoMember(3)]
        public string[] PoolItemCodes { get; set; }

        /// <summary>The won item action item ID.</summary>
        [ProtoMember(4)]
        public string ResultItemId { get; set; }

        /// <summary>The won item display name (with quality).</summary>
        [ProtoMember(5)]
        public string ResultItemName { get; set; }

        /// <summary>The won item base code for icon rendering.</summary>
        [ProtoMember(6)]
        public string ResultItemCode { get; set; }

        /// <summary>Quality name of the result (e.g. "СИЯЮЩЕЕ").</summary>
        [ProtoMember(7)]
        public string ResultQualityName { get; set; }

        /// <summary>Quality color hex (e.g. "#A78BFA").</summary>
        [ProtoMember(8)]
        public string ResultQualityColor { get; set; }

        /// <summary>Unique claim token to prevent double-claiming.</summary>
        [ProtoMember(9)]
        public string ClaimToken { get; set; }
    }

    /// <summary>
    /// Client → Server: player claims the case reward after animation finishes.
    /// </summary>
    [ProtoContract]
    public class ClaimCaseRewardMessage
    {
        [ProtoMember(1)]
        public string ClaimToken { get; set; }
    }
}
