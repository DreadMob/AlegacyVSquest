using ProtoBuf;

namespace VsQuest
{
    /// <summary>
    /// Message from client to server to claim reroll reward after animation.
    /// </summary>
    [ProtoContract]
    public class ClaimRerollRewardMessage
    {
        /// <summary>
        /// The group ID that was rerolled
        /// </summary>
        [ProtoMember(1)]
        public string GroupId { get; set; }
    }
}
