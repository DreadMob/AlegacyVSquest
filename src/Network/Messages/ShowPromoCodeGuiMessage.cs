using ProtoBuf;

namespace VsQuest
{
    /// <summary>
    /// Server → Client: Tells the client to open the promo code GUI.
    /// </summary>
    [ProtoContract]
    public class ShowPromoCodeGuiMessage
    {
    }
}
