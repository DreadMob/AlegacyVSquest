using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class KillActionTargetObjective : ActionObjectiveBase
    {
        private const string EventName = "killactiontarget";

        public string CountKey(string questId, string objectiveId) => $"vsquest:{EventName}:{questId}:{objectiveId}:count";

        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out string objectiveId, out _, out int needed)) return false;
            if (needed <= 0) return true;

            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return false;

            int have = wa.GetInt(CountKey(questId, objectiveId), 0);
            return have >= needed;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out string objectiveId, out _, out int needed)) return new List<int> { 0, 0 };

            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return new List<int> { 0, needed };

            int have = wa.GetInt(CountKey(questId, objectiveId), 0);
            if (needed < 0) needed = 0;
            if (have > needed) have = needed;

            return new List<int> { have, needed };
        }

        protected bool TryParseArgs(string[] args, out string questId, out string objectiveId, out string targetId, out int needed)
        {
            questId = null;
            objectiveId = null;
            targetId = null;
            needed = 0;

            if (args == null || args.Length < 4) return false;

            questId = args[0];
            objectiveId = args[1];
            targetId = args[2];

            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId) || string.IsNullOrWhiteSpace(targetId)) return false;

            if (!int.TryParse(args[3], out needed)) needed = 0;
            if (needed < 0) needed = 0;

            return true;
        }
    }
}
