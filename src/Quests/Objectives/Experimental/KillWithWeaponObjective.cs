using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    /// <summary>
    /// [EXPERIMENTAL] Counts kills made with a specific weapon type/code.
    /// Args: [0] questId, [1] objectiveId, [2] weaponCode (supports * wildcard suffix), [3] need
    /// </summary>
    public class KillWithWeaponObjective : ActionObjectiveBase
    {
        private const string EventName = "killwithweapon";

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
        /// Called externally when a kill is confirmed. Checks if the held weapon matches.
        /// </summary>
        public void TryIncrement(IPlayer byPlayer, string heldItemCode, string questId, string objectiveId, string weaponCode, int need)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;
            if (string.IsNullOrWhiteSpace(heldItemCode)) return;

            if (!WeaponCodeMatches(heldItemCode, weaponCode)) return;

            var wa = byPlayer.Entity.WatchedAttributes;
            string key = HaveKey(questId, objectiveId);
            int have = wa.GetInt(key, 0);
            if (have >= need) return;

            have++;
            wa.SetInt(key, have);
            wa.MarkPathDirty(key);
        }

        private static bool WeaponCodeMatches(string heldCode, string weaponCode)
        {
            if (string.IsNullOrWhiteSpace(weaponCode) || weaponCode == "*") return true;

            if (weaponCode.EndsWith("*"))
            {
                string prefix = weaponCode.Substring(0, weaponCode.Length - 1);
                return heldCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(heldCode, weaponCode, StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryParseArgs(string[] args, out string questId, out string objectiveId, out string weaponCode, out int need)
        {
            questId = null;
            objectiveId = null;
            weaponCode = "*";
            need = 0;

            if (args == null || args.Length < 4) return false;

            questId = args[0];
            objectiveId = args[1];
            weaponCode = args[2];

            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId)) return false;

            if (!int.TryParse(args[3], out need)) need = 0;
            if (need < 0) need = 0;

            return true;
        }
    }
}
