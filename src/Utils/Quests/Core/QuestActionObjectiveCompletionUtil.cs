using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestActionObjectiveCompletionUtil
    {
        private static string CompletedKey(string questId, int stageIndex, string objectiveKey) => $"alegacyvsquest:ao:completed:{questId}:stage{stageIndex}:{objectiveKey}";
        private static string SequenceStepKey(string questId, string sequenceId) => $"vsquest:sequence:{questId}:{sequenceId}:step";

        public static void ResetCompletionFlags(Quest quest, IServerPlayer player)
        {
            if (quest == null || player?.Entity?.WatchedAttributes == null) return;

            var wa = player.Entity.WatchedAttributes;

            // Reset for all stages if quest has stages, otherwise use legacy objectives
            if (quest.HasStages)
            {
                for (int stageIndex = 0; stageIndex < quest.stages.Count; stageIndex++)
                {
                    var actionObjectives = quest.GetActionObjectives(stageIndex);
                    ResetActionObjectiveFlags(quest, stageIndex, actionObjectives, wa);
                }
            }
            else
            {
                ResetActionObjectiveFlags(quest, 0, quest.actionObjectives, wa);
            }
        }

        private static void ResetActionObjectiveFlags(Quest quest, int stageIndex, List<ActionWithArgs> actionObjectives, Vintagestory.API.Datastructures.ITreeAttribute wa)
        {
            if (actionObjectives == null || actionObjectives.Count == 0) return;

            foreach (var ao in actionObjectives)
            {
                if (ao == null || string.IsNullOrWhiteSpace(ao.id)) continue;

                string objectiveKey;

                if (!string.IsNullOrWhiteSpace(ao.objectiveId))
                {
                    objectiveKey = ao.objectiveId;
                }
                else if (ao.id == "interactat" && ao.args != null && ao.args.Length >= 1 && QuestInteractAtUtil.TryParsePos(ao.args[0], out int x, out int y, out int z))
                {
                    objectiveKey = QuestInteractAtUtil.InteractionKey(x, y, z);
                }
                else
                {
                    objectiveKey = ao.id;
                }

                string key = CompletedKey(quest.id, stageIndex, objectiveKey);
                wa.RemoveAttribute(key);

                // Reset interactwithentity counters
                if (ao.id == "interactwithentity" && ao.args != null && ao.args.Length >= 2)
                {
                    string questId = ao.args[0];
                    string target = ao.args[1];
                    if (!string.IsNullOrWhiteSpace(questId) && !string.IsNullOrWhiteSpace(target))
                    {
                        string counterKey = InteractWithEntityObjective.CountKey(questId, target);
                        wa.RemoveAttribute(counterKey);
                    }
                }

                // Reset killactiontarget counters
                if (ao.id == "killactiontarget" && ao.args != null && ao.args.Length >= 2)
                {
                    string questId = ao.args[0];
                    string objectiveId = ao.args[1];
                    if (!string.IsNullOrWhiteSpace(questId) && !string.IsNullOrWhiteSpace(objectiveId))
                    {
                        string counterKey = $"vsquest:killactiontarget:{questId}:{objectiveId}:count";
                        wa.RemoveAttribute(counterKey);
                    }
                }

                if (ao.id == "sequence" && ao.args != null && ao.args.Length >= 2)
                {
                    string questId = ao.args[0];
                    string sequenceId = ao.args[1];
                    if (!string.IsNullOrWhiteSpace(questId) && !string.IsNullOrWhiteSpace(sequenceId))
                    {
                        string stepKey = SequenceStepKey(questId, sequenceId);
                        wa.RemoveAttribute(stepKey);
                    }
                }
            }
        }

        public static void TryFireOnComplete(ICoreServerAPI sapi, IServerPlayer player, ActiveQuest activeQuest, ActionWithArgs objectiveDef, string objectiveKey, bool isNowCompletable)
        {
            if (sapi == null || player == null || activeQuest == null || objectiveDef == null) return;
            if (!isNowCompletable) return;
            if (string.IsNullOrWhiteSpace(activeQuest.questId)) return;

            objectiveKey = string.IsNullOrWhiteSpace(objectiveKey)
                ? (string.IsNullOrWhiteSpace(objectiveDef.objectiveId) ? objectiveDef.id : objectiveDef.objectiveId)
                : objectiveKey;

            var wa = player.Entity?.WatchedAttributes;
            if (wa == null) return;

            // Use stage-aware completion key
            string key = CompletedKey(activeQuest.questId, activeQuest.currentStageIndex, objectiveKey);
            if (wa.GetBool(key, false))
            {
                sapi.Logger.Debug($"[QuestActionObjectiveCompletionUtil] Objective {objectiveKey} already completed for quest {activeQuest.questId} stage {activeQuest.currentStageIndex}");
                return;
            }

            string actionString = objectiveDef.onCompleteActions;

            sapi.Logger.Debug($"[QuestActionObjectiveCompletionUtil] Firing onComplete for {objectiveKey} in quest {activeQuest.questId} stage {activeQuest.currentStageIndex}");

            wa.SetBool(key, true);
            wa.MarkPathDirty(key);

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            string defaultSound = questSystem?.Config?.defaultObjectiveCompletionSound;

            if (string.IsNullOrWhiteSpace(actionString))
            {
                if (!string.IsNullOrWhiteSpace(defaultSound))
                {
                    sapi.World.PlaySoundFor(new AssetLocation(defaultSound), player);
                }
                return;
            }

            if (string.Equals(actionString.Trim(), "nosound", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var msg = new QuestAcceptedMessage { questGiverId = activeQuest.questGiverId, questId = activeQuest.questId };
            ActionStringExecutor.Execute(sapi, msg, player, actionString);

            // If objective actions didn't explicitly include playsound, play the default completion sound.
            if (!string.IsNullOrWhiteSpace(defaultSound)
                && actionString.IndexOf("playsound", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                sapi.World.PlaySoundFor(new AssetLocation(defaultSound), player);
            }
        }
    }
}
