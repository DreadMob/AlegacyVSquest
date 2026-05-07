using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using System.Text.RegularExpressions;
using System.Linq;

namespace VsQuest
{
    public class CheckVariableObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (args.Length < 3) return false;

            string varName = args[0];
            string op = args[1];
            string valueStr = args[2];

            int playerValue = byPlayer.Entity.WatchedAttributes.GetInt(varName, 0);
            if (!int.TryParse(valueStr, out int requiredValue))
            {
                return false;
            }

            switch (op)
            {
                case "=":
                case "==":
                    return playerValue == requiredValue;
                case ">":
                    return playerValue > requiredValue;
                case ">=":
                    return playerValue >= requiredValue;
                case "<":
                    return playerValue < requiredValue;
                case "<=":
                    return playerValue <= requiredValue;
                case "!=":
                    return playerValue != requiredValue;
                default:
                    return false;
            }
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (args == null || args.Length < 3 || byPlayer?.Entity?.WatchedAttributes == null)
            {
                return new List<int>(new int[] { IsCompletable(byPlayer, args) ? 1 : 0 });
            }

            string varName = args[0];
            string op = args[1];
            string valueStr = args[2];

            if (string.IsNullOrWhiteSpace(varName) || string.IsNullOrWhiteSpace(op) || string.IsNullOrWhiteSpace(valueStr))
            {
                return new List<int>(new int[] { IsCompletable(byPlayer, args) ? 1 : 0 });
            }

            int have = byPlayer.Entity.WatchedAttributes.GetInt(varName, 0);
            if (!int.TryParse(valueStr, out int need))
            {
                return new List<int>(new int[] { IsCompletable(byPlayer, args) ? 1 : 0 });
            }

            // For numeric comparisons, expose have/need so UI can show proper progress (e.g. 3/4).
            // For non-monotonic comparisons (e.g. !=), fall back to boolean progress.
            if (op == ">" || op == ">=" || op == "<" || op == "<=" || op == "==" || op == "=")
            {
                if (need < 0) need = 0;
                if (have < 0) have = 0;
                return new List<int>(new int[] { have, need });
            }

            return new List<int>(new int[] { IsCompletable(byPlayer, args) ? 1 : 0 });
        }

        public void CheckAndFire(IServerPlayer player, Quest quest, ActiveQuest activeQuest, int objectiveIndex, ICoreServerAPI sapi)
        {
            string[] args = quest.actionObjectives[objectiveIndex].args;
            if (args.Length < 4) return;

            string firedAttribute = $"{activeQuest.questId}-{objectiveIndex}-fired";
            if (player.Entity.WatchedAttributes.GetBool(firedAttribute, false))
            {
                return;
            }

            if (IsCompletable(player, args))
            {
                string actionsToFire = args[3];
                var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();

                var actionStrings = actionsToFire.Split(';');

                foreach (var actionString in actionStrings)
                {
                    if (string.IsNullOrWhiteSpace(actionString)) continue;

                    var matches = Regex.Matches(actionString.Trim(), @"(?:'([^']*)')|([^\\s]+)");
                    if (matches.Count == 0) continue;

                    var actionId = matches[0].Value;
                    var actionArgs = new List<string>();

                    for (int i = 1; i < matches.Count; i++)
                    {
                        if (matches[i].Groups[1].Success)
                        {
                            actionArgs.Add(matches[i].Groups[1].Value);
                        }
                        else
                        {
                            actionArgs.Add(matches[i].Groups[2].Value);
                        }
                    }

                    if (questSystem.ActionRegistry.TryGetValue(actionId, out var action))
                    {
                        var message = new QuestAcceptedMessage { questGiverId = activeQuest.questGiverId, questId = activeQuest.questId };
                        action.Execute(sapi, message, player, actionArgs.ToArray());
                    }
                }

                var objectiveDef = quest.actionObjectives[objectiveIndex];
                QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, player, activeQuest, objectiveDef, objectiveDef.objectiveId, true);

                player.Entity.WatchedAttributes.SetBool(firedAttribute, true);
            }
        }
    }
}
