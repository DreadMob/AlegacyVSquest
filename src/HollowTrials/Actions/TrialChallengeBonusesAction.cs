using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Quest reward action: evaluates challenges and grants bonuses.
    /// This is the SINGLE point of reward granting (reputation, shards, first-kill bonus).
    /// HollowTrialSystem.Combat only broadcasts and stores quality.
    /// Usage in quest JSON: { "id": "trialchallengebonuses", "args": [] }
    /// </summary>
    public class TrialChallengeBonusesAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            if (sapi == null || player == null) return;

            var trialSystem = sapi.ModLoader.GetModSystem<HollowTrialSystem>();
            if (trialSystem == null) return;

            // Find which trial quest this is for
            string questId = message?.questId;
            if (string.IsNullOrWhiteSpace(questId)) return;

            var config = trialSystem.FindConfigByQuestId(questId, out int matchedTier);
            if (config == null || matchedTier == 0) return;

            // Get combat tracker for this boss
            var tracker = trialSystem.GetCombatTracker(config.trialKey);
            if (tracker == null || !tracker.IsFinished) return;

            // SOLO ENFORCEMENT: if multiple players participated, no rewards
            if (tracker.DamageByPlayer.Count > 1)
            {
                tracker.Reset();
                return;
            }

            string playerUid = player.PlayerUID;

            // Evaluate challenges for the specific tier
            var challenges = config.GetChallenges(matchedTier);
            var completedChallenges = TrialChallengeEvaluator.Evaluate(challenges, tracker, playerUid);

            // Process rewards via reputation manager (with active modifier)
            var repManager = trialSystem.GetReputationManager();
            var activeModifier = (TrialModifierType)(trialSystem.GetActiveModifier());
            repManager?.ProcessBossKill(playerUid, config, matchedTier, completedChallenges, activeModifier);

            // First kill bonus notification
            if (repManager != null && !repManager.HasKilledBefore(playerUid, config.trialKey))
            {
                // ProcessBossKill already recorded it, but the check inside happens before recording
                string firstKillMsg = LocalizationUtils.GetSafe("albase:trial-first-kill-bonus");
                sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, firstKillMsg, EnumChatType.Notification);
            }

            // Notify player about challenges
            if (completedChallenges.Count > 0)
            {
                string challengeList = string.Join(", ", completedChallenges);
                int bonus = TrialChallengeEvaluator.CalculateChallengeReputation(matchedTier, completedChallenges.Count);
                string msg = LocalizationUtils.GetSafe("albase:trial-challenges-completed", challengeList, bonus);
                sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
            }

            // Roll quality for the reward item
            int quality = TrialQualityRoller.Roll(matchedTier, completedChallenges, sapi.World.Rand);
            player.Entity.WatchedAttributes.SetInt("alegacyvsquest:trial:lastRewardQuality", quality);
            player.Entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:trial:lastRewardQuality");

            // Record personal best stats
            double durationMinutes = tracker.GetFightDurationMinutes();
            bool wasDeathless = !tracker.DeathsByPlayer.ContainsKey(playerUid) || tracker.DeathsByPlayer[playerUid] == 0;
            repManager?.RecordBestResult(playerUid, config.trialKey, matchedTier, durationMinutes, completedChallenges.Count, wasDeathless);

            // Increment kill count (for progressive difficulty)
            repManager?.IncrementKillCount(playerUid, config.trialKey);

            // Bonus shards from kill count scaling
            int killCount = repManager?.GetKillCount(playerUid, config.trialKey) ?? 0;
            int bonusShards = TrialReputationManager.GetKillCountBonusShards(killCount);
            if (bonusShards > 0)
            {
                repManager?.AddVoidShards(playerUid, bonusShards);
            }

            // Clean up tracker
            tracker.Reset();
        }
    }
}
