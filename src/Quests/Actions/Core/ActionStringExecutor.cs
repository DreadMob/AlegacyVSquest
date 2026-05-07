using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class ActionStringExecutor
    {
        public static void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string actionString)
        {
            if (sapi == null || player == null) return;
            if (string.IsNullOrWhiteSpace(actionString)) return;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return;

            var actionStrings = actionString.Split(';').Select(s => s.Trim());

            foreach (var singleAction in actionStrings)
            {
                if (string.IsNullOrWhiteSpace(singleAction)) continue;

                var matches = Regex.Matches(singleAction, "(?:'([^']*)')|([^\\s]+)");
                if (matches.Count == 0) continue;

                var actionId = matches[0].Value;
                var args = new List<string>();

                for (int i = 1; i < matches.Count; i++)
                {
                    if (matches[i].Groups[1].Success)
                    {
                        args.Add(matches[i].Groups[1].Value);
                    }
                    else
                    {
                        args.Add(matches[i].Groups[2].Value);
                    }
                }

                if (questSystem.ActionRegistry.TryGetValue(actionId, out var action))
                {
                    action.Execute(sapi, message, player, args.ToArray());
                }
                else
                {
                    sapi.Logger.Error($"[vsquest] ActionStringExecutor: Unknown action ID '{actionId}' in quest '{message?.questId}'.");
                }
            }
        }
    }
}
