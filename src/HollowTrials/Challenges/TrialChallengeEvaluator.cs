using System;
using System.Collections.Generic;

namespace VsQuest
{
    /// <summary>
    /// Evaluates which challenges a player completed after killing a trial boss.
    /// </summary>
    public static class TrialChallengeEvaluator
    {
        /// <summary>
        /// Evaluate all challenges for a boss kill and return the list of completed challenge types.
        /// </summary>
        public static List<string> Evaluate(
            HollowTrialConfig config,
            TrialCombatTracker tracker,
            string playerUid)
        {
            var completed = new List<string>();
            if (config?.challenges == null || tracker == null || string.IsNullOrWhiteSpace(playerUid))
                return completed;

            foreach (var challenge in config.challenges)
            {
                if (challenge == null || string.IsNullOrWhiteSpace(challenge.type)) continue;

                bool passed = challenge.type.ToLowerInvariant() switch
                {
                    "speedkill" => EvaluateSpeedkill(tracker, challenge),
                    "deathless" => EvaluateDeathless(tracker, playerUid),
                    "nopotions" => EvaluateNoPotions(tracker, playerUid),
                    "lowgear" => EvaluateLowGear(tracker, playerUid, challenge),
                    "perfectdodge" => EvaluatePerfectDodge(tracker, playerUid, challenge),
                    _ => false
                };

                if (passed)
                {
                    completed.Add(challenge.type);
                }
            }

            return completed;
        }

        private static bool EvaluateSpeedkill(TrialCombatTracker tracker, HollowTrialChallenge challenge)
        {
            if (challenge.thresholdMinutes <= 0) return false;
            double durationMinutes = tracker.GetFightDurationMinutes();
            return durationMinutes > 0 && durationMinutes <= challenge.thresholdMinutes;
        }

        private static bool EvaluateDeathless(TrialCombatTracker tracker, string playerUid)
        {
            if (!tracker.DeathsByPlayer.TryGetValue(playerUid, out int deaths))
                return true; // No deaths recorded = deathless

            return deaths == 0;
        }

        private static bool EvaluateNoPotions(TrialCombatTracker tracker, string playerUid)
        {
            if (!tracker.PotionUsageByPlayer.TryGetValue(playerUid, out int usage))
                return true; // No usage recorded = no potions

            return usage == 0;
        }

        private static bool EvaluateLowGear(TrialCombatTracker tracker, string playerUid, HollowTrialChallenge challenge)
        {
            if (challenge.maxArmorTier <= 0) return false;

            if (!tracker.MaxArmorTierByPlayer.TryGetValue(playerUid, out int maxTier))
                return true; // No armor recorded = passes

            return maxTier <= challenge.maxArmorTier;
        }

        private static bool EvaluatePerfectDodge(TrialCombatTracker tracker, string playerUid, HollowTrialChallenge challenge)
        {
            if (string.IsNullOrWhiteSpace(challenge.abilityCode)) return false;

            if (!tracker.AbilityDamageByPlayer.TryGetValue(playerUid, out var abilities))
                return true; // No ability damage recorded = perfect dodge

            return !abilities.Contains(challenge.abilityCode);
        }

        /// <summary>
        /// Calculate bonus reputation for completed challenges based on boss tier.
        /// </summary>
        public static int CalculateChallengeReputation(int tier, int completedCount)
        {
            if (completedCount <= 0) return 0;

            int perChallenge = tier switch
            {
                1 => 10,
                2 => 15,
                3 => 25,
                _ => 10
            };

            return perChallenge * completedCount;
        }
    }
}
