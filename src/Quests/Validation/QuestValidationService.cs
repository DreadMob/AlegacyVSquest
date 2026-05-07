using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace VsQuest
{
    public interface IQuestValidationService
    {
        bool IsValidQuest(Quest quest);
        bool IsValidQuestId(string questId);
        bool HasValidObjectives(Quest quest);
        bool HasValidRewards(Quest quest);
        IEnumerable<string> ValidateQuest(Quest quest);
    }

    public class QuestValidationService : IQuestValidationService
    {
        private readonly IQuestContext _questContext;

        public QuestValidationService(IQuestContext questContext)
        {
            _questContext = questContext;
        }

        public bool IsValidQuest(Quest quest)
        {
            if (quest == null) return false;
            return IsValidQuestId(quest.id);
        }

        public bool IsValidQuestId(string questId)
        {
            return !string.IsNullOrWhiteSpace(questId);
        }

        public bool HasValidObjectives(Quest quest)
        {
            if (quest == null) return false;
            
            var objectives = quest.gatherObjectives ?? quest.killObjectives ?? quest.blockPlaceObjectives ?? quest.blockBreakObjectives;
            return objectives != null && objectives.Count > 0;
        }

        public bool HasValidRewards(Quest quest)
        {
            if (quest == null) return false;
            return quest.itemRewards != null && quest.itemRewards.Count > 0;
        }

        public IEnumerable<string> ValidateQuest(Quest quest)
        {
            var errors = new List<string>();

            if (!IsValidQuest(quest))
            {
                errors.Add("Quest is null or has invalid ID");
                return errors;
            }

            if (!HasValidObjectives(quest) && !HasValidRewards(quest))
            {
                errors.Add($"Quest '{quest.id}' has no valid objectives or rewards");
            }

            // Validate stages if present
            if (quest.HasStages && quest.stages != null)
            {
                for (int i = 0; i < quest.stages.Count; i++)
                {
                    var stage = quest.stages[i];
                    if (stage == null)
                    {
                        errors.Add($"Quest '{quest.id}' stage {i} is null");
                        continue;
                    }

                    if (stage.gatherObjectives == null && stage.killObjectives == null && 
                        stage.blockPlaceObjectives == null && stage.blockBreakObjectives == null &&
                        (stage.actionObjectives == null || stage.actionObjectives.Count == 0))
                    {
                        errors.Add($"Quest '{quest.id}' stage {i} has no objectives");
                    }
                }
            }

            return errors;
        }
    }
}
