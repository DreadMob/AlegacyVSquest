using System.Collections.Generic;

namespace VsQuest
{
    public class QuestConfig
    {
        public bool CloseGuiAfterAcceptingAndCompleting { get; set; } = true;
        public string defaultObjectiveCompletionSound { get; set; } = "sounds/tutorialstepsuccess";
        public string killObjectiveProgressSound { get; set; } = "albase:sounds/priest_target";
        public float killObjectiveProgressSoundPitch { get; set; } = 0.45f;
        public float killObjectiveProgressSoundVolume { get; set; } = 0.40f;
        public string killObjectiveCompleteSound { get; set; } = "sounds/tutorialstepsuccess";
        public float killObjectiveCompleteSoundPitch { get; set; } = 1f;
        public float killObjectiveCompleteSoundVolume { get; set; } = 1.2f;
        public bool ShowCustomBossDeathMessage { get; set; } = false;
    }

    /// <summary>
    /// Represents a single stage within a quest.
    /// Quests can have multiple stages that unlock sequentially.
    /// </summary>
    public class QuestStage
    {
        /// <summary>
        /// Language key for the stage title displayed in progress text
        /// </summary>
        public string stageTitleLangKey { get; set; }

        /// <summary>
        /// Objectives that must be completed to finish this stage
        /// </summary>
        public List<Objective> gatherObjectives { get; set; } = new List<Objective>();
        public List<Objective> killObjectives { get; set; } = new List<Objective>();
        public List<Objective> blockPlaceObjectives { get; set; } = new List<Objective>();
        public List<Objective> blockBreakObjectives { get; set; } = new List<Objective>();
        public List<Objective> interactObjectives { get; set; } = new List<Objective>();
        public List<ActionWithArgs> actionObjectives { get; set; } = new List<ActionWithArgs>();

        /// <summary>
        /// Actions executed when this stage is completed (before advancing to next stage)
        /// </summary>
        public List<ActionWithArgs> onStageCompleteActions { get; set; } = new List<ActionWithArgs>();

        /// <summary>
        /// Sound played when stage is completed
        /// </summary>
        public string stageCompleteSound { get; set; }
    }

    public class Quest
    {
        public string id { get; set; }
        public int cooldown { get; set; }
        public bool perPlayer { get; set; }
        public string predecessor { get; set; }
        public List<string> predecessors { get; set; } = new List<string>();
        public string killObjectiveCompleteSound { get; set; }
        public float? killObjectiveCompleteSoundPitch { get; set; }
        public float? killObjectiveCompleteSoundVolume { get; set; }
        public List<ActionWithArgs> onAcceptedActions { get; set; } = new List<ActionWithArgs>();

        /// <summary>
        /// If true (or null), broadcasts a chat message when the quest is completed.
        /// If false, no completion message is sent.
        /// Defaults to the global config value if not specified.
        /// </summary>
        public bool? notifyOnComplete { get; set; }

        // Legacy objectives (for backward compatibility - treated as single stage)
        public List<Objective> gatherObjectives { get; set; } = new List<Objective>();
        public List<Objective> killObjectives { get; set; } = new List<Objective>();
        public List<Objective> blockPlaceObjectives { get; set; } = new List<Objective>();
        public List<Objective> blockBreakObjectives { get; set; } = new List<Objective>();
        public List<Objective> interactObjectives { get; set; } = new List<Objective>();
        public List<ActionWithArgs> actionObjectives { get; set; } = new List<ActionWithArgs>();

        /// <summary>
        /// Quest stages for multi-stage quests. When populated, legacy objective lists are ignored.
        /// </summary>
        public List<QuestStage> stages { get; set; } = new List<QuestStage>();

        public List<ItemReward> itemRewards { get; set; } = new List<ItemReward>();
        public RandomItemReward randomItemRewards { get; set; } = new RandomItemReward();
        public List<ActionWithArgs> actionRewards { get; set; } = new List<ActionWithArgs>();
        public List<QuestReputationRequirement> reputationRequirements { get; set; } = new List<QuestReputationRequirement>();

        /// <summary>
        /// Returns true if this quest uses the new stage system
        /// </summary>
        public bool HasStages => stages != null && stages.Count > 0;

        /// <summary>
        /// Gets the objectives for a specific stage, or legacy objectives if no stages defined
        /// </summary>
        public QuestStage GetStage(int stageIndex)
        {
            if (HasStages && stageIndex >= 0 && stageIndex < stages.Count)
            {
                return stages[stageIndex];
            }
            if (stageIndex == 0)
            {
                // Return legacy objectives as a synthetic stage
                return new QuestStage
                {
                    gatherObjectives = gatherObjectives ?? new List<Objective>(),
                    killObjectives = killObjectives ?? new List<Objective>(),
                    blockPlaceObjectives = blockPlaceObjectives ?? new List<Objective>(),
                    blockBreakObjectives = blockBreakObjectives ?? new List<Objective>(),
                    interactObjectives = interactObjectives ?? new List<Objective>(),
                    actionObjectives = actionObjectives ?? new List<ActionWithArgs>()
                };
            }
            return null;
        }

        /// <summary>
        /// Gets the total number of stages (1 for legacy quests)
        /// </summary>
        public int StageCount => HasStages ? stages.Count : 1;

        /// <summary>
        /// Gets action objectives for the specified stage index.
        /// Returns stage-specific action objectives if quest has stages, otherwise returns root action objectives.
        /// </summary>
        /// <param name="stageIndex">The stage index (0-based)</param>
        /// <returns>List of action objectives for the stage, or empty list if stage doesn't exist</returns>
        public List<ActionWithArgs> GetActionObjectives(int stageIndex)
        {
            var stage = GetStage(stageIndex);
            // For multi-stage quests, return stage-specific action objectives
            // For single-stage quests (stage is null), return root-level actionObjectives
            return stage?.actionObjectives ?? actionObjectives ?? new List<ActionWithArgs>();
        }
    }

    public class Objective
    {
        public List<string> validCodes { get; set; }
        public int demand { get; set; }
        public List<string> positions { get; set; }
        public bool removeAfterFinished { get; set; }
        public List<ActionWithArgs> actionRewards { get; set; } = new List<ActionWithArgs>();

        // Sound properties for kill objectives
        public string completeSound { get; set; }
        public float completePitch { get; set; } = 1f;
        public float completeVolume { get; set; } = 1f;
    }

    public class ItemReward
    {
        public string itemCode { get; set; }
        public int amount { get; set; }
    }

    public class ActionWithArgs
    {
        public string id { get; set; }
        public string objectiveId { get; set; }
        public string onCompleteActions { get; set; }
        public string[] args { get; set; } = new string[0];
    }

    public class RandomItemReward
    {
        public int selectAmount { get; set; }
        public List<RandomItem> items { get; set; } = new List<RandomItem>();
    }

    public class RandomItem
    {
        public string itemCode { get; set; }
        public int minAmount { get; set; }
        public int maxAmount { get; set; }
    }

    public class QuestReputationRequirement
    {
        public string scope { get; set; }
        public string id { get; set; }
        public int minValue { get; set; }
        public string rankLangKey { get; set; }
    }
}