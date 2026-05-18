using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    /// <summary>
    /// [EXPERIMENTAL] Counts fish caught (items obtained via fishing mechanic).
    /// Args: [0] questId, [1] objectiveId, [2] fishCode (supports * wildcard suffix, default "*" = any fish), [3] need
    /// 
    /// Fish detection: checks if the obtained item code contains "fish" or matches the specified code.
    /// Increment is called externally when a fishing catch event is detected.
    /// </summary>
    public class FishCatchObjective : ActionObjectiveBase
    {
        private const string EventName = "fishcatch";

        public static string HaveKey(string questId, string objectiveId) => $"vsquest:{EventName}:{questId}:{objectiveId}:have";

        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out string objectiveId, out _, out int need)) return false;
            if (need <= 0) return true;

            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return false;

            int have = wa.GetInt(HaveKey(questId, objectiveId), 0);
            return have >= need;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out string objectiveId, out _, out int need)) return new List<int> { 0, 0 };

            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return new List<int> { 0, need };

            int have = wa.GetInt(HaveKey(questId, objectiveId), 0);
            if (need < 0) need = 0;
            if (have > need) have = need;

            return new List<int> { have, need };
        }

        /// <summary>
        /// Called when a fish is caught. Checks if it matches the target code.
        /// </summary>
        public void TryIncrement(IPlayer byPlayer, string caughtItemCode, string questId, string objectiveId, string fishCode, int need)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;
            if (string.IsNullOrWhiteSpace(caughtItemCode)) return;

            if (!FishCodeMatches(caughtItemCode, fishCode)) return;

            var wa = byPlayer.Entity.WatchedAttributes;
            string key = HaveKey(questId, objectiveId);
            int have = wa.GetInt(key, 0);
            if (have >= need) return;

            have++;
            wa.SetInt(key, have);
            wa.MarkPathDirty(key);
        }

        /// <summary>
        /// Checks if an item code looks like a fish item (heuristic).
        /// </summary>
        public static bool IsFishItem(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return false;
            return itemCode.Contains("fish", StringComparison.OrdinalIgnoreCase)
                || itemCode.Contains("catfish", StringComparison.OrdinalIgnoreCase)
                || itemCode.Contains("bass", StringComparison.OrdinalIgnoreCase)
                || itemCode.Contains("perch", StringComparison.OrdinalIgnoreCase)
                || itemCode.Contains("pike", StringComparison.OrdinalIgnoreCase)
                || itemCode.Contains("carp", StringComparison.OrdinalIgnoreCase)
                || itemCode.Contains("trout", StringComparison.OrdinalIgnoreCase);
        }

        private static bool FishCodeMatches(string caughtCode, string fishCode)
        {
            if (string.IsNullOrWhiteSpace(fishCode) || fishCode == "*")
            {
                // Any fish — use heuristic
                return IsFishItem(caughtCode);
            }

            if (fishCode.EndsWith("*"))
            {
                string prefix = fishCode.Substring(0, fishCode.Length - 1);
                return caughtCode.Contains(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return caughtCode.Contains(fishCode, StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryParseArgs(string[] args, out string questId, out string objectiveId, out string fishCode, out int need)
        {
            questId = null;
            objectiveId = null;
            fishCode = "*";
            need = 0;

            if (args == null || args.Length < 4) return false;

            questId = args[0];
            objectiveId = args[1];
            fishCode = args[2];

            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId)) return false;

            if (!int.TryParse(args[3], out need)) need = 0;
            if (need < 0) need = 0;

            return true;
        }
    }
}
