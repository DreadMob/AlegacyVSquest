using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// [EXPERIMENTAL] Time-limit gate: tracks elapsed time since quest acceptance.
    /// Completes (passes) while elapsed time is WITHIN the limit. Fails if time runs out.
    /// 
    /// Use as a gate objective alongside other objectives to create timed challenges.
    /// 
    /// Args: [0] questId, [1] objectiveId, [2] timeLimitSeconds
    /// 
    /// The start time is stored when the quest is accepted (use "resettimer" action).
    /// IsCompletable returns true if elapsed time &lt;= timeLimitSeconds.
    /// Progress shows remaining seconds / total seconds.
    /// </summary>
    public class TimerObjective : ActionObjectiveBase
    {
        private const string EventName = "timer";

        public static string StartTimeKey(string questId, string objectiveId) => $"vsquest:{EventName}:{questId}:{objectiveId}:start";

        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out string objectiveId, out int timeLimitSeconds)) return false;
            if (timeLimitSeconds <= 0) return true;

            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return false;

            double startTime = wa.GetDouble(StartTimeKey(questId, objectiveId), 0);
            if (startTime <= 0) return true; // Timer not started yet — allow progress

            var sapi = byPlayer.Entity.Api as ICoreServerAPI;
            double nowSeconds = GetWorldTimeSeconds(sapi);
            if (nowSeconds <= 0) return true;

            double elapsed = nowSeconds - startTime;
            return elapsed <= timeLimitSeconds;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out string questId, out string objectiveId, out int timeLimitSeconds)) return new List<int> { 0, 0 };

            var wa = byPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return new List<int> { timeLimitSeconds, timeLimitSeconds };

            double startTime = wa.GetDouble(StartTimeKey(questId, objectiveId), 0);
            if (startTime <= 0) return new List<int> { timeLimitSeconds, timeLimitSeconds };

            var sapi = byPlayer.Entity.Api as ICoreServerAPI;
            double nowSeconds = GetWorldTimeSeconds(sapi);
            if (nowSeconds <= 0) return new List<int> { timeLimitSeconds, timeLimitSeconds };

            double elapsed = nowSeconds - startTime;
            int remaining = Math.Max(0, timeLimitSeconds - (int)elapsed);

            return new List<int> { remaining, timeLimitSeconds };
        }

        /// <summary>
        /// Starts/resets the timer for a player. Call from "resettimer" action on quest accept.
        /// </summary>
        public static void StartTimer(IPlayer byPlayer, string questId, string objectiveId, ICoreServerAPI sapi)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;

            double now = GetWorldTimeSeconds(sapi);
            if (now <= 0) return;

            var wa = byPlayer.Entity.WatchedAttributes;
            string key = StartTimeKey(questId, objectiveId);
            wa.SetDouble(key, now);
            wa.MarkPathDirty(key);
        }

        private static double GetWorldTimeSeconds(ICoreServerAPI sapi)
        {
            if (sapi?.World?.Calendar == null) return 0;
            return sapi.World.Calendar.TotalHours * 3600.0;
        }

        public static bool TryParseArgs(string[] args, out string questId, out string objectiveId, out int timeLimitSeconds)
        {
            questId = null;
            objectiveId = null;
            timeLimitSeconds = 0;

            if (args == null || args.Length < 3) return false;

            questId = args[0];
            objectiveId = args[1];

            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId)) return false;

            if (!int.TryParse(args[2], out timeLimitSeconds)) timeLimitSeconds = 0;
            if (timeLimitSeconds < 0) timeLimitSeconds = 0;

            return true;
        }
    }
}
