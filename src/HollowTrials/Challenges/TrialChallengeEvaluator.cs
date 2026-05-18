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
            List<HollowTrialChallenge> challenges,
            TrialCombatTracker tracker,
            string playerUid)
        {
            var completed = new List<string>();
            if (challenges == null || challenges.Count == 0 || tracker == null || string.IsNullOrWhiteSpace(playerUid))
                return completed;

            foreach (var challenge in challenges)
            {
                if (challenge == null || string.IsNullOrWhiteSpace(challenge.type)) continue;

                bool passed = challenge.type.ToLowerInvariant() switch
                {
                    "speedkill" => EvaluateSpeedkill(tracker, challenge),
                    "deathless" => EvaluateDeathless(tracker, playerUid),
                    "nofood" => EvaluateNoFood(tracker, playerUid),
                    "nopotions" => EvaluateNoPotions(tracker, playerUid),
                    "lowmaxhp" => EvaluateLowMaxHp(tracker, playerUid),
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

        private static bool EvaluateNoFood(TrialCombatTracker tracker, string playerUid)
        {
            if (!tracker.SaturationGainedByPlayer.TryGetValue(playerUid, out bool gained))
                return true; // No saturation gain recorded = no food eaten

            return !gained;
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
        /// No potions: player did not heal during the fight (no health increase from items).
        /// Uses the same saturation tracking — if player healed, they likely used a potion.
        /// For now, tracks via a separate flag in combat tracker.
        /// </summary>
        private static bool EvaluateNoPotions(TrialCombatTracker tracker, string playerUid)
        {
            if (!tracker.PotionUsedByPlayer.TryGetValue(playerUid, out bool used))
                return true; // No potion use recorded

            return !used;
        }

        /// <summary>
        /// Low diet: player's max HP must be 16 or below (poor nutrition = low max health).
        /// In VS, diet affects max HP — bad diet means fewer hearts.
        /// </summary>
        private static bool EvaluateLowMaxHp(TrialCombatTracker tracker, string playerUid)
        {
            // Check max HP recorded during fight — must be <= 16
            if (!tracker.MaxHpByPlayer.TryGetValue(playerUid, out float maxHp))
                return true; // No data = assume passed

            return maxHp <= 16f;
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
