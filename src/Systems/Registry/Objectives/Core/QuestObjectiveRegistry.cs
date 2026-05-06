using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class QuestObjectiveRegistry : IRegistry
    {
        private readonly Dictionary<string, ActionObjectiveBase> objectiveRegistry;
        private readonly ICoreAPI api;

        public QuestObjectiveRegistry(Dictionary<string, ActionObjectiveBase> objectiveRegistry, ICoreAPI api)
        {
            this.objectiveRegistry = objectiveRegistry;
            this.api = api;
        }

        public void Register()
        {
            objectiveRegistry.Add("plantflowers", new NearbyFlowersActionObjective());
            objectiveRegistry.Add("hasattribute", new PlayerHasAttributeActionObjective());
            objectiveRegistry.Add("interactat", new InteractAtCoordinateObjective());
            objectiveRegistry.Add("interactcount", new InteractCountObjective());
            objectiveRegistry.Add("reachwaypoint", new ReachWaypointObjective());
            objectiveRegistry.Add("hasitem", new HasItemObjective());
            objectiveRegistry.Add("wearing", new WearingObjective());
            objectiveRegistry.Add("interactwithentity", new InteractWithEntityObjective());
            objectiveRegistry.Add("inland", new InLandObjective());
            objectiveRegistry.Add("landgate", new LandGateObjective());
            objectiveRegistry.Add("killnear", new KillNearObjective());
            objectiveRegistry.Add("killactiontarget", new KillActionTargetObjective());
            objectiveRegistry.Add("sequence", new SequenceObjective());
            objectiveRegistry.Add("temporalstorm", new TemporalStormObjective());
            objectiveRegistry.Add("checkvariable", new CheckVariableObjective());
            objectiveRegistry.Add("randomkill", new RandomKillObjective());
            objectiveRegistry.Add("walkdistance", new WalkDistanceObjective());
            objectiveRegistry.Add("timeofday", new TimeOfDayObjective());
        }
    }
}
