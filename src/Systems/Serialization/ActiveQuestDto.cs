using System.Collections.Generic;
using ProtoBuf;

namespace VsQuest
{
    /// <summary>
    /// Data transfer object for ActiveQuest serialization.
    /// Simplified version without legacy EventTracker fields.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ActiveQuestDto
    {
        public long questGiverId { get; set; }
        public string questId { get; set; }

        public bool IsCompletableOnClient { get; set; }
        public bool IsCurrentStageCompleteOnClient { get; set; }
        public string ProgressText { get; set; }

        public int currentStageIndex { get; set; }
        public List<int> completedStageIndices { get; set; } = new List<int>();

        public static ActiveQuestDto FromDomain(ActiveQuest quest)
        {
            if (quest == null) return null;

            return new ActiveQuestDto
            {
                questGiverId = quest.questGiverId,
                questId = quest.questId,
                IsCompletableOnClient = quest.ClientState?.IsCompletableOnClient ?? false,
                IsCurrentStageCompleteOnClient = quest.ClientState?.IsCurrentStageCompleteOnClient ?? false,
                ProgressText = quest.ClientState?.ProgressText ?? string.Empty,
                currentStageIndex = quest.currentStageIndex,
                completedStageIndices = quest.completedStageIndices ?? new List<int>()
            };
        }

        public ActiveQuest ToDomain(IQuestStateManager stateManager = null)
        {
            var quest = new ActiveQuest
            {
                questGiverId = questGiverId,
                questId = questId,
                currentStageIndex = currentStageIndex,
                completedStageIndices = completedStageIndices ?? new List<int>(),
                ClientState = new ActiveQuestClientState
                {
                    IsCompletableOnClient = IsCompletableOnClient,
                    IsCurrentStageCompleteOnClient = IsCurrentStageCompleteOnClient,
                    ProgressText = ProgressText ?? string.Empty
                }
            };
            
            if (stateManager != null)
            {
                quest.SetStateManager(stateManager);
            }
            
            return quest;
        }
    }
}
