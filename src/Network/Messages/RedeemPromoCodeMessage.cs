using ProtoBuf;

namespace VsQuest
{
    /// <summary>
    /// Client → Server: Player submits a promo code for redemption.
    /// </summary>
    [ProtoContract]
    public class RedeemPromoCodeMessage
    {
        [ProtoMember(1)]
        public string Code { get; set; }
    }
}
