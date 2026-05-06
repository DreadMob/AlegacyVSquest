using System.Collections.Generic;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class RollKillObjectivesAction : EntityActionBase
    {
        protected override int MinArgs => 6; // randomKillQuestUtils.ParseRollArgsMulti expectations
        protected override string ActionName => "rollkillobjectives";

        protected override void ExecuteAction(ICoreServerAPI api, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;


            RandomKillQuestUtils.ParseRollArgsMulti(args, out int objectiveCount, out int minCount, out int maxCount, out string template, out string progressActions, out string completeActions, out int mobListStartIndex);

            var mobCodes = RandomKillQuestUtils.ReadMobCodes(args, mobListStartIndex);
            if (mobCodes.Count == 0)
            {
                throw new QuestException("The 'rollkillobjectives' action requires at least 1 entityCode.");
            }
            if (objectiveCount > mobCodes.Count)
            {
                throw new QuestException($"The 'rollkillobjectives' action requires at least {objectiveCount} distinct entityCodes, but only {mobCodes.Count} were provided.");
            }

            string questId = message?.questId;
            if (string.IsNullOrEmpty(questId)) return;
            
            string slotsKey = $"alegacyvsquest:randkill:{questId}:slots";

            byPlayer.Entity.WatchedAttributes.SetInt(slotsKey, objectiveCount);
            byPlayer.Entity.WatchedAttributes.MarkPathDirty(slotsKey);

            RandomKillQuestUtils.StoreQuestActionStrings(byPlayer, questId, progressActions, completeActions);

            var remaining = new List<string>(mobCodes);

            for (int slot = 0; slot < objectiveCount; slot++)
            {
                int need = api.World.Rand.Next(minCount, maxCount + 1);
                int idx = api.World.Rand.Next(0, remaining.Count);
                string code = remaining[idx];
                remaining.RemoveAt(idx);

                string codeKey = $"alegacyvsquest:randkill:{questId}:slot{slot}:code";
                string needKey = $"alegacyvsquest:randkill:{questId}:slot{slot}:need";
                string haveKey = $"alegacyvsquest:randkill:{questId}:slot{slot}:have";

                byPlayer.Entity.WatchedAttributes.SetString(codeKey, code);
                byPlayer.Entity.WatchedAttributes.SetInt(needKey, need);
                byPlayer.Entity.WatchedAttributes.SetInt(haveKey, 0);
                byPlayer.Entity.WatchedAttributes.MarkPathDirty(codeKey);
                byPlayer.Entity.WatchedAttributes.MarkPathDirty(needKey);
                byPlayer.Entity.WatchedAttributes.MarkPathDirty(haveKey);
            }
        }
    }
}
