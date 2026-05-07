using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class KillNearObjective : ActionObjectiveBase
    {
        public static string HaveKey(string questId, string objectiveId) => $"vsquest:killnear:{questId}:{objectiveId}:have";

        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out _, out _, out _, out _, out int need)) return false;
            if (need <= 0) return true;

            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return false;

            string objectiveId = ExtractObjectiveId(args);
            if (string.IsNullOrWhiteSpace(objectiveId)) return false;

            int have = wa.GetInt(HaveKey(questId, objectiveId), 0);
            return have >= need;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out _, out _, out _, out _, out int need)) return new List<int>(new int[] { 0, 0 });

            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return new List<int>(new int[] { 0, need });

            string objectiveId = ExtractObjectiveId(args);
            if (string.IsNullOrWhiteSpace(objectiveId)) return new List<int>(new int[] { 0, need });

            int have = wa.GetInt(HaveKey(questId, objectiveId), 0);
            if (need < 0) need = 0;
            if (have > need) have = need;

            return new List<int>(new int[] { have, need });
        }

        // Args format:
        // [0] questId
        // [1] objectiveId (must match actionObjective.objectiveId)
        // [2] x,y,z
        // [3] radius
        // [4] mobCode (optional, default "*")
        // [5] need
        public static bool TryParseArgs(string[] args, out string questId, out string objectiveId, out int x, out int y, out int z, out double radius, out string mobCode, out int need)
        {
            questId = null;
            objectiveId = null;
            x = y = z = 0;
            radius = 0;
            mobCode = "*";
            need = 0;

            if (args == null || args.Length < 6) return false;

            questId = args[0];
            objectiveId = args[1];
            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId)) return false;

            if (!TryParsePos(args[2], out x, out y, out z)) return false;

            if (!double.TryParse(args[3], out radius)) radius = 0;
            if (radius < 0) radius = 0;

            if (!string.IsNullOrWhiteSpace(args[4])) mobCode = args[4];
            if (!int.TryParse(args[5], out need)) need = 0;
            if (need < 0) need = 0;

            return true;
        }

        private static bool TryParseArgs(string[] args, out string questId, out int x, out int y, out int z, out double radius, out int need)
        {
            questId = null;
            x = y = z = 0;
            radius = 0;
            need = 0;

            if (args == null || args.Length < 6) return false;

            questId = args[0];
            if (string.IsNullOrWhiteSpace(questId)) return false;

            if (!TryParsePos(args[2], out x, out y, out z)) return false;

            if (!double.TryParse(args[3], out radius)) radius = 0;
            if (radius < 0) radius = 0;

            if (!int.TryParse(args[5], out need)) need = 0;
            if (need < 0) need = 0;

            return true;
        }

        private static string ExtractObjectiveId(string[] args)
        {
            if (args == null || args.Length < 2) return null;
            return args[1];
        }

        private static bool TryParsePos(string pos, out int x, out int y, out int z)
        {
            x = y = z = 0;
            if (string.IsNullOrWhiteSpace(pos)) return false;

            var parts = pos.Split(',');
            if (parts.Length != 3) return false;

            return int.TryParse(parts[0].Trim(), out x)
                && int.TryParse(parts[1].Trim(), out y)
                && int.TryParse(parts[2].Trim(), out z);
        }
    }
}
