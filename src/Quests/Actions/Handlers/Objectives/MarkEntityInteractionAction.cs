using System;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class MarkEntityInteractionAction : PlayerActionBase
    {
        protected override int MinArgs => 3;
        protected override string ActionName => "markentityinteraction";

        // Args:
        // [0] questId
        // [1] target entity id or entity code (must match objective args[1])
        // [2] objectiveId (must match objective.objectiveId)
        // Optional gate:
        // [3] requiredIntKey (player watched attribute int key)
        // [4] requiredMinValue (int)
        protected override void Execute(ICoreServerAPI sapi, IServerPlayer byPlayer, string[] args)
        {
            string questId = args[0];
            string target = args[1];
            string objectiveId = args[2];

            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(objectiveId)) return;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem?.QuestRegistry == null) return;

            var activeQuests = questSystem.GetPlayerQuests(byPlayer.PlayerUID);
            if (activeQuests == null || activeQuests.Count == 0) return;

            // Find any active quest that has matching interactwithentity objective
            foreach (var activeQuest in activeQuests)
            {
                if (activeQuest == null || string.IsNullOrWhiteSpace(activeQuest.questId)) continue;
                if (!questSystem.QuestRegistry.TryGetValue(activeQuest.questId, out var questDef)) continue;

                // Get action objectives from current stage using centralized method
                var actionObjectivesToCheck = questDef?.GetActionObjectives(activeQuest.currentStageIndex);
                
                if (actionObjectivesToCheck == null || actionObjectivesToCheck.Count == 0) continue;

                // Find matching interactwithentity objective
                ActionWithArgs objective = null;
                for (int i = 0; i < actionObjectivesToCheck.Count; i++)
                {
                    var ao = actionObjectivesToCheck[i];
                    if (ao == null) continue;
                    if (!string.Equals(ao.id, "interactwithentity", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(ao.objectiveId, objectiveId, StringComparison.OrdinalIgnoreCase)) continue;
                    if (ao.args == null || ao.args.Length < 3) continue;
                    // Match by target and ensure args[0] matches the active quest
                    if (!string.Equals(ao.args[0], activeQuest.questId, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(ao.args[1]?.Trim(), target.Trim(), StringComparison.OrdinalIgnoreCase)) continue;

                    objective = ao;
                    break;
                }

                if (objective == null) continue;

                var wa = byPlayer.Entity?.WatchedAttributes;
                if (wa == null) continue;

                // Optional gating: only allow counting when player has required int >= min value.
                if (args.Length >= 5 && !string.IsNullOrWhiteSpace(args[3]))
                {
                    string requiredKey = args[3];
                    if (!int.TryParse(args[4], out int requiredMin))
                    {
                        continue;
                    }

                    int have = wa.GetInt(requiredKey, 0);
                    if (have < requiredMin)
                    {
                        continue;
                    }
                }

                string key = InteractWithEntityObjective.CountKey(activeQuest.questId, target);
                int cur = wa.GetInt(key, 0);
                wa.SetInt(key, cur + 1);
                wa.MarkPathDirty(key);

                // If this objective is now complete, fire onCompleteActions
                if (questSystem.ActionObjectiveRegistry != null
                    && questSystem.ActionObjectiveRegistry.TryGetValue("interactwithentity", out var impl)
                    && impl != null
                    && impl.IsCompletable(byPlayer, objective.args))
                {
                    QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, byPlayer, activeQuest, objective, objective.objectiveId, true);
                }

                return; // Found and processed
            }
        }
    }
}
