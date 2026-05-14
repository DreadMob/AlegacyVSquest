using ProtoBuf;

namespace VsQuest
{
    /// <summary>
    /// Server → Client: Result of a promo code redemption attempt.
    /// </summary>
    [ProtoContract]
    public class PromoCodeResultMessage
    {
        /// <summary>Whether the code was successfully redeemed.</summary>
        [ProtoMember(1)]
        public bool Success { get; set; }

        /// <summary>Lang key or message to display to the player.</summary>
        [ProtoMember(2)]
        public string Message { get; set; }
    }
}
