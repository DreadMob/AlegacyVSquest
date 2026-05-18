using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    /// <summary>
    /// [EXPERIMENTAL] Counts blocks placed matching a code pattern.
    /// Args: [0] questId, [1] objectiveId, [2] blockCode (supports * wildcard suffix), [3] need
    /// </summary>
    public class PlaceBlockObjective : ActionObjectiveBase
    {
        private const string EventName = "placeblock";

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
        /// Called when a block is placed. Checks if it matches the target code.
        /// </summary>
        public void TryIncrement(IPlayer byPlayer, string placedBlockCode, string questId, string objectiveId, string targetBlockCode, int need)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;
            if (string.IsNullOrWhiteSpace(placedBlockCode)) return;

            if (!BlockCodeMatches(placedBlockCode, targetBlockCode)) return;

            var wa = byPlayer.Entity.WatchedAttributes;
            string key = HaveKey(questId, objectiveId);
            int have = wa.GetInt(key, 0);
            if (have >= need) return;

            have++;
            wa.SetInt(key, have);
            wa.MarkPathDirty(key);
        }

        private static bool BlockCodeMatches(string placedCode, string targetCode)
        {
            if (string.IsNullOrWhiteSpace(targetCode) || targetCode == "*") return true;

            if (targetCode.EndsWith("*"))
            {
                string prefix = targetCode.Substring(0, targetCode.Length - 1);
                return placedCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    || placedCode.Contains(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(placedCode, targetCode, StringComparison.OrdinalIgnoreCase)
                || placedCode.Contains(targetCode, StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryParseArgs(string[] args, out string questId, out string objectiveId, out string blockCode, out int need)
        {
            questId = null;
            objectiveId = null;
            blockCode = "*";
            need = 0;

            if (args == null || args.Length < 4) return false;

            questId = args[0];
            objectiveId = args[1];
            blockCode = args[2];

            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId)) return false;

            if (!int.TryParse(args[3], out need)) need = 0;
            if (need < 0) need = 0;

            return true;
        }
    }
}
