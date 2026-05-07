using ProtoBuf;

namespace VsQuest
{
    /// <summary>
    /// Client-side UI state for ActiveQuest.
    /// Separated from core quest data to reduce ActiveQuest responsibilities.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ActiveQuestClientState
    {
        /// <summary>
        /// Whether the quest can be completed on the client (for UI display).
        /// </summary>
        public bool IsCompletableOnClient { get; set; }

        /// <summary>
        /// Whether the current stage is complete on the client (for UI display).
        /// </summary>
        public bool IsCurrentStageCompleteOnClient { get; set; }

        /// <summary>
        /// Progress text for UI display.
        /// </summary>
        public string ProgressText { get; set; }

        public ActiveQuestClientState()
        {
            IsCompletableOnClient = false;
            IsCurrentStageCompleteOnClient = false;
            ProgressText = string.Empty;
        }
    }
}
