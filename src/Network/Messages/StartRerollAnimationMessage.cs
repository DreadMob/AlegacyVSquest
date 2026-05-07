using ProtoBuf;

namespace VsQuest
{
    /// <summary>
    /// Message from server to client to start reroll animation.
    /// Contains all possible items and the result.
    /// </summary>
    [ProtoContract]
    public class StartRerollAnimationMessage
    {
        /// <summary>
        /// All possible item IDs that could appear in animation
        /// </summary>
        [ProtoMember(1)]
        public string[] ItemIds { get; set; }

        /// <summary>
        /// Display names for items (parallel to ItemIds)
        /// </summary>
        [ProtoMember(2)]
        public string[] ItemNames { get; set; }

        /// <summary>
        /// The actual result item ID
        /// </summary>
        [ProtoMember(3)]
        public string ResultItemId { get; set; }

        /// <summary>
        /// The actual result item display name
        /// </summary>
        [ProtoMember(4)]
        public string ResultItemName { get; set; }

        /// <summary>
        /// Animation type ID (e.g., "simplespin")
        /// </summary>
        [ProtoMember(5)]
        public string AnimationType { get; set; }

        /// <summary>
        /// Group ID for claiming reward after animation
        /// </summary>
        [ProtoMember(6)]
        public string GroupId { get; set; }
    }
}
