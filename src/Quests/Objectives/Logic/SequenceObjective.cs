using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class SequenceObjective : ActionObjectiveBase
    {
        public static string StepKey(string questId, string sequenceId) => $"vsquest:sequence:{questId}:{sequenceId}:step";

        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out string sequenceId, out string[] steps)) return false;

            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return false;

            int stepIndex = wa.GetInt(StepKey(questId, sequenceId), 0);
            if (stepIndex < 0) stepIndex = 0;

            // Completed if step index is past end
            if (stepIndex >= steps.Length) return true;

            // Try to advance on server if current step is already satisfied
            TryAdvanceServer(byPlayer, questId, sequenceId, steps, ref stepIndex);

            return stepIndex >= steps.Length;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out string sequenceId, out string[] steps)) return new List<int>(new int[] { 0, 0 });

            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return new List<int>(new int[] { 0, steps.Length });

            int stepIndex = wa.GetInt(StepKey(questId, sequenceId), 0);
            if (stepIndex < 0) stepIndex = 0;

            TryAdvanceServer(byPlayer, questId, sequenceId, steps, ref stepIndex);

            int have = stepIndex;
            int need = steps.Length;
            if (have > need) have = need;

            return new List<int>(new int[] { have, need });
        }

        // Args format:
        // [0] questId
        // [1] sequenceId
        // [2..] objectiveIds of actionObjectives in required order
        private static bool TryParseArgs(string[] args, out string questId, out string sequenceId, out string[] steps)
        {
            questId = null;
            sequenceId = null;
            steps = Array.Empty<string>();

            if (args == null || args.Length < 3) return false;

            questId = args[0];
            sequenceId = args[1];
            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(sequenceId)) return false;

            int count = args.Length - 2;
            if (count <= 0) return false;

            steps = new string[count];
            for (int i = 0; i < count; i++)
            {
                steps[i] = args[i + 2];
            }

            return true;
        }

        private static void TryAdvanceServer(IPlayer byPlayer, string questId, string sequenceId, string[] steps, ref int stepIndex)
        {
            if (byPlayer?.Entity?.Api is not ICoreServerAPI) return;

            var wa = byPlayer.Entity.WatchedAttributes;
            if (wa == null) return;

            // Advance as long as the current step is satisfied
            int safeGuard = 0;
            while (stepIndex < steps.Length && safeGuard < 32)
            {
                safeGuard++;

                if (!IsStepCompletable(byPlayer, questId, steps[stepIndex])) break;

                stepIndex++;
                wa.SetInt(StepKey(questId, sequenceId), stepIndex);
                wa.MarkPathDirty(StepKey(questId, sequenceId));
            }
        }

        private static bool IsStepCompletable(IPlayer byPlayer, string questId, string stepObjectiveId)
        {
            if (string.IsNullOrWhiteSpace(stepObjectiveId)) return false;

            var questSystem = byPlayer?.Entity?.Api?.ModLoader?.GetModSystem<QuestSystem>();
            if (questSystem == null) return false;

            if (!questSystem.QuestRegistry.TryGetValue(questId, out var questDef) || questDef == null) return false;

            // Get the active quest to determine current stage
            var playerQuests = questSystem.GetPlayerQuests(byPlayer.PlayerUID);
            if (playerQuests == null) return false;

            ActiveQuest activeQuest = null;
            foreach (var q in playerQuests)
            {
                if (string.Equals(q.questId, questId, StringComparison.OrdinalIgnoreCase))
                {
                    activeQuest = q;
                    break;
                }
            }

            if (activeQuest == null) return false;

            // Use stage-aware action objectives
            var stageActionObjectives = questDef.GetActionObjectives(activeQuest.currentStageIndex);
            if (stageActionObjectives == null) return false;

            ActionWithArgs target = null;
            for (int i = 0; i < stageActionObjectives.Count; i++)
            {
                var ao = stageActionObjectives[i];
                if (ao == null) continue;
                if (!string.Equals(ao.objectiveId, stepObjectiveId, StringComparison.OrdinalIgnoreCase)) continue;
                target = ao;
                break;
            }

            if (target == null) return false;

            if (!questSystem.ActionObjectiveRegistry.TryGetValue(target.id, out var impl) || impl == null) return false;

            return impl.IsCompletable(byPlayer, target.args);
        }
    }
}
