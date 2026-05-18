using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    /// <summary>
    /// [EXPERIMENTAL] Counts crop harvests (breaking mature crop blocks).
    /// Args: [0] questId, [1] objectiveId, [2] cropCode (supports * wildcard suffix, e.g. "crop-flax*"), [3] need
    /// </summary>
    public class HarvestCropObjective : ActionObjectiveBase
    {
        private const string EventName = "harvestcrop";

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
        /// Called when a block is broken. Checks if it's a mature crop matching the code.
        /// </summary>
        public void TryIncrement(IPlayer byPlayer, string blockCode, string questId, string objectiveId, string cropCode, int need)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;
            if (string.IsNullOrWhiteSpace(blockCode)) return;

            if (!IsCropBlock(blockCode)) return;
            if (!CropCodeMatches(blockCode, cropCode)) return;

            var wa = byPlayer.Entity.WatchedAttributes;
            string key = HaveKey(questId, objectiveId);
            int have = wa.GetInt(key, 0);
            if (have >= need) return;

            have++;
            wa.SetInt(key, have);
            wa.MarkPathDirty(key);
        }

        private static bool IsCropBlock(string blockCode)
        {
            if (string.IsNullOrWhiteSpace(blockCode)) return false;
            // VS crop blocks typically contain "crop-" in their code
            return blockCode.Contains("crop-", StringComparison.OrdinalIgnoreCase);
        }

        private static bool CropCodeMatches(string blockCode, string cropCode)
        {
            if (string.IsNullOrWhiteSpace(cropCode) || cropCode == "*") return true;

            if (cropCode.EndsWith("*"))
            {
                string prefix = cropCode.Substring(0, cropCode.Length - 1);
                return blockCode.Contains(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return blockCode.Contains(cropCode, StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryParseArgs(string[] args, out string questId, out string objectiveId, out string cropCode, out int need)
        {
            questId = null;
            objectiveId = null;
            cropCode = "*";
            need = 0;

            if (args == null || args.Length < 4) return false;

            questId = args[0];
            objectiveId = args[1];
            cropCode = args[2];

            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId)) return false;

            if (!int.TryParse(args[3], out need)) need = 0;
            if (need < 0) need = 0;

            return true;
        }
    }
}
