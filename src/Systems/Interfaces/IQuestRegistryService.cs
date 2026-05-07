using System.Collections.Generic;

namespace VsQuest
{
    public interface IQuestRegistryService
    {
        Dictionary<string, Quest> QuestRegistry { get; }
        Dictionary<string, IQuestAction> ActionRegistry { get; }
        Dictionary<string, ActionObjectiveBase> ActionObjectiveRegistry { get; }
    }
}
