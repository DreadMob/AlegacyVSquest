using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Dynamic objective for trial quests: checks if any active trial boss has been killed solo.
    /// Usage in quest JSON: { "id": "killactivetrial", "args": ["tier"] }
    /// where tier = "1", "2", or "3" (determines which tier quest the player took).
    /// 
    /// IsCompletable returns true when any active trial boss has been killed solo
    /// and this player has kill credit.
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

            // Check all trial configs — boss may have been spawned from any anchor
            var allConfigs = trialSystem.GetAllConfigs();
            if (allConfigs == null || allConfigs.Count == 0) return false;

            foreach (var cfg in allConfigs)
            {
                if (cfg == null || string.IsNullOrWhiteSpace(cfg.trialKey)) continue;

                var tracker = trialSystem.GetCombatTracker(cfg.trialKey);
                if (tracker == null || !tracker.IsFinished) continue;

                // TIER CHECK: boss must have been spawned at the matching tier
                if (tracker.SpawnTier != tier) continue;

                // SOLO ENFORCEMENT: if multiple players participated, kill is void
                if (tracker.DamageByPlayer.Count > 1) continue;

                // Check if this player has kill credit
                string creditPlayer = tracker.GetKillCreditPlayer(sapi);
                if (string.Equals(creditPlayer, byPlayer.PlayerUID, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            // Progress: [current, target] — 0/1 or 1/1
            bool complete = IsCompletable(byPlayer, args);
            return new List<int> { complete ? 1 : 0, 1 };
        }
    }
}
