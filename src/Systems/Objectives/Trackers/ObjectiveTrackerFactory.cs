using System.Collections.Generic;

namespace VsQuest
{
    /// <summary>
    /// Factory for creating objective trackers from quest objectives.
    /// Centralizes tracker creation logic.
    /// </summary>
    public static class ObjectiveTrackerFactory
    {
        /// <summary>
        /// Create trackers for a quest (legacy or current stage)
        /// </summary>
        public static List<IObjectiveTracker> CreateTrackersForQuest(Quest quest, int stageIndex = 0)
        {
            var trackers = new List<IObjectiveTracker>();
            if (quest == null) return trackers;
            
            var stage = quest.GetStage(stageIndex);
            
            // Kill objectives
            var killObjectives = stage?.killObjectives ?? quest.killObjectives;
            foreach (var objective in killObjectives)
            {
                trackers.Add(new KillObjectiveTracker(
                    objective.demand,
                    objective.validCodes,
                    objective.completeSound,
                    objective.completePitch,
                    objective.completeVolume));
            }
            
            // Gather objectives
            var gatherObjectives = stage?.gatherObjectives ?? quest.gatherObjectives;
            foreach (var objective in gatherObjectives)
            {
                trackers.Add(new GatherObjectiveTracker(objective.demand, objective.validCodes));
            }
            
            // Block place objectives
            var blockPlaceObjectives = stage?.blockPlaceObjectives ?? quest.blockPlaceObjectives;
            foreach (var objective in blockPlaceObjectives)
            {
                trackers.Add(new BlockObjectiveTracker(
                    BlockObjectiveType.Place,
                    objective.demand,
                    objective.validCodes,
                    objective.positions));
            }
            
            // Block break objectives
            var blockBreakObjectives = stage?.blockBreakObjectives ?? quest.blockBreakObjectives;
            foreach (var objective in blockBreakObjectives)
            {
                trackers.Add(new BlockObjectiveTracker(
                    BlockObjectiveType.Break,
                    objective.demand,
                    objective.validCodes,
                    objective.positions));
            }
            
            // Interact objectives
            var interactObjectives = stage?.interactObjectives ?? quest.interactObjectives;
            foreach (var objective in interactObjectives)
            {
                trackers.Add(new BlockObjectiveTracker(
                    BlockObjectiveType.Interact,
                    objective.demand,
                    objective.validCodes,
                    objective.positions));
            }
            
            return trackers;
        }
        
        /// <summary>
        /// Create action objective trackers from action objectives
        /// </summary>
        public static List<IObjectiveTracker> CreateActionTrackers(List<ActionWithArgs> actionObjectives, Dictionary<string, ActionObjectiveBase> registry)
        {
            var trackers = new List<IObjectiveTracker>();
            if (actionObjectives == null || registry == null) return trackers;
            
            foreach (var action in actionObjectives)
            {
                if (action == null) continue;
                if (registry.TryGetValue(action.id, out var implementation))
                {
                    trackers.Add(new ActionObjectiveTracker(implementation, action.args));
                }
            }
            
            return trackers;
        }
    }
}
