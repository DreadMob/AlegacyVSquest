using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Dynamic objective for trial quests: checks if the active trial boss of the given tier is dead.
    /// Usage in quest JSON: { "id": "killactivetrial", "args": ["tier"] }
    /// where tier = "1", "2", or "3".
    /// 
    /// IsCompletable returns true when the active trial boss of that tier has been killed
    /// (tracked via TrialCombatTracker.IsFinished).
    /// </summary>
    public class KillActiveTrialObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (byPlayer == null || args == null || args.Length < 1) return false;

            if (!int.TryParse(args[0], out int tier)) return false;

            var sapi = byPlayer.Entity?.Api as ICoreServerAPI;
            if (sapi == null) return false;

            var trialSystem = sapi.ModLoader.GetModSystem<HollowTrialSystem>();
            if (trialSystem == null) return false;

            // Find the active trial key for this tier
            string trialKey = ResolveActiveTrialKey(trialSystem, tier);
            if (string.IsNullOrWhiteSpace(trialKey)) return false;

            // Check if the combat tracker shows the fight is finished
            var tracker = trialSystem.GetCombatTracker(trialKey);
            if (tracker == null || !tracker.IsFinished) return false;

            // Check if this player has kill credit
            string creditPlayer = tracker.GetKillCreditPlayer(sapi);
            return string.Equals(creditPlayer, byPlayer.PlayerUID, StringComparison.OrdinalIgnoreCase);
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            // Progress: [current, target] — 0/1 or 1/1
            bool complete = IsCompletable(byPlayer, args);
            return new List<int> { complete ? 1 : 0, 1 };
        }

        /// <summary>
        /// Resolve the active trial key for the given tier.
        /// </summary>
        public static string ResolveActiveTrialKey(HollowTrialSystem trialSystem, int tier)
        {
            if (trialSystem == null) return null;

            var activeKeys = trialSystem.GetActiveTrialKeys();
            if (activeKeys == null) return null;

            foreach (var key in activeKeys)
            {
                var cfg = trialSystem.FindConfig(key);
                if (cfg != null && cfg.tier == tier)
                    return key;
            }

            return null;
        }
    }
}
