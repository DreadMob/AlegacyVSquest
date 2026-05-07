using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class InteractWithEntityObjective : ActionObjectiveBase
    {
        public static string CountKey(string questId, long entityId) => CountKey(questId, entityId.ToString());

        public static string CountKey(string questId, string targetIdOrCode)
        {
            targetIdOrCode = targetIdOrCode?.Trim()?.ToLowerInvariant();
            return $"vsquest:interactentity:{questId}:{targetIdOrCode}:count";
        }

        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out string targetIdOrCode, out int need))
            {
                try
                {
                    byPlayer?.Entity?.Api?.Logger?.Warning($"[alegacyvsquest] interactwithentity objective has invalid args (args='{string.Join("|", args ?? Array.Empty<string>())}'). Treating as completable to avoid stuck quest.");
                }
                catch (Exception)
                {
                    // Swallow - this is a fail-safe logging attempt
                }

                // Fail-open: an invalid objective should not permanently block quest turn-in.
                return true;
            }
            int have = GetHave(byPlayer, questId, targetIdOrCode);
            return have >= need;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out string targetIdOrCode, out int need)) return new List<int>(new int[] { 0, 1 });

            int have = GetHave(byPlayer, questId, targetIdOrCode);
            if (have > need) have = need;
            if (need < 0) need = 0;

            return new List<int>(new int[] { have, need });
        }

        private static int GetHave(IPlayer byPlayer, string questId, string targetIdOrCode)
        {
            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return 0;

            return wa.GetInt(CountKey(questId, targetIdOrCode), 0);
        }

        private static bool TryParseArgs(string[] args, out string questId, out string targetIdOrCode, out int need)
        {
            questId = null;
            targetIdOrCode = null;
            need = 0;

            if (args == null || args.Length < 3) return false;

            questId = args[0];
            if (string.IsNullOrWhiteSpace(questId)) return false;

            targetIdOrCode = args[1];
            if (string.IsNullOrWhiteSpace(targetIdOrCode)) return false;

            if (!int.TryParse(args[2], out need)) need = 0;
            if (need < 0) need = 0;

            return true;
        }
    }
}
