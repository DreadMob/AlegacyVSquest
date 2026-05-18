using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    /// <summary>
    /// [EXPERIMENTAL] Counts items smelted/produced in a furnace or crucible.
    /// Args: [0] questId, [1] objectiveId, [2] itemCode (supports * wildcard suffix), [3] need
    /// 
    /// Increment is called externally when a smelt output is detected
    /// (e.g. player picks up from furnace output slot).
    /// </summary>
    public class SmeltItemObjective : ActionObjectiveBase
    {
        private const string EventName = "smeltitem";

        public static string HaveKey(string questId, string objectiveId) =>
            $"vsquest:{EventName}:{questId}:{objectiveId}:have";

        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out string objectiveId, out _, out int need))
                return false;
            if (need <= 0) return true;

            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return false;

            int have = wa.GetInt(HaveKey(questId, objectiveId), 0);
            return have >= need;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out string objectiveId, out _, out int need))
                return new List<int> { 0, 0 };

            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return new List<int> { 0, need };

            int have = wa.GetInt(HaveKey(questId, objectiveId), 0);
            if (need < 0) need = 0;
            if (have > need) have = need;

            return new List<int> { have, need };
        }

        /// <summary>
        /// Called when a smelt output is obtained. Checks if it matches the target.
        /// </summary>
        public void TryIncrement(IPlayer byPlayer, string smeltedItemCode, int amount,
            string questId, string objectiveId, string targetItemCode, int need)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;
            if (string.IsNullOrWhiteSpace(smeltedItemCode)) return;
            if (amount <= 0) amount = 1;

            if (!ItemCodeMatches(smeltedItemCode, targetItemCode)) return;

            var wa = byPlayer.Entity.WatchedAttributes;
            string key = HaveKey(questId, objectiveId);
            int have = wa.GetInt(key, 0);
            if (have >= need) return;

            have += amount;
            if (have > need) have = need;
            wa.SetInt(key, have);
            wa.MarkPathDirty(key);
        }

        private static bool ItemCodeMatches(string smeltedCode, string targetCode)
        {
            if (string.IsNullOrWhiteSpace(targetCode) || targetCode == "*") return true;

            if (targetCode.EndsWith("*"))
            {
                string prefix = targetCode.Substring(0, targetCode.Length - 1);
                return smeltedCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(smeltedCode, targetCode, StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryParseArgs(string[] args, out string questId,
            out string objectiveId, out string itemCode, out int need)
        {
            questId = null;
            objectiveId = null;
            itemCode = "*";
            need = 0;

            if (args == null || args.Length < 4) return false;

            questId = args[0];
            objectiveId = args[1];
            itemCode = args[2];

            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId))
                return false;

            if (!int.TryParse(args[3], out need)) need = 0;
            if (need < 0) need = 0;

            return true;
        }
    }
}
