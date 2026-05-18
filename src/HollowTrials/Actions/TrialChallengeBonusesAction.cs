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

            // Determine the tier from questId
            int matchedTier = 0;
            if (questId.Contains("tier1")) matchedTier = 1;
            else if (questId.Contains("tier2")) matchedTier = 2;
            else if (questId.Contains("tier3")) matchedTier = 3;
            if (matchedTier == 0) return;

            // Find the finished tracker that matches this tier
            HollowTrialConfig config = null;
            TrialCombatTracker tracker = null;
            string playerUid = player.PlayerUID;

            var allConfigs = trialSystem.GetAllConfigs();
            foreach (var cfg in allConfigs)
            {
                if (cfg == null) continue;
                var t = trialSystem.GetCombatTracker(cfg.trialKey);
                if (t == null || !t.IsFinished) continue;
                if (t.SpawnTier != matchedTier) continue;
                if (t.DamageByPlayer.Count > 1) continue;

                string creditPlayer = t.GetKillCreditPlayer(sapi);
                if (string.Equals(creditPlayer, playerUid, StringComparison.OrdinalIgnoreCase))
                {
                    config = cfg;
                    tracker = t;
                    break;
                }
            }

            if (config == null || tracker == null) return;

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
                var localizedChallenges = new List<string>();
                foreach (var ch in completedChallenges)
                {
                    string localized = LocalizationUtils.GetSafe($"albase:trial-challenge-{ch}");
                    localizedChallenges.Add(string.Equals(localized, $"albase:trial-challenge-{ch}", StringComparison.OrdinalIgnoreCase) ? ch : localized);
                }
                string challengeList = string.Join(", ", localizedChallenges);
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
