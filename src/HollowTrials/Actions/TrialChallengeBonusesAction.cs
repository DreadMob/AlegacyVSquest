using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Quest reward action: evaluates challenges and grants bonuses.
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

            var config = trialSystem.FindConfigByQuestId(questId);
            if (config == null) return;

            // Get combat tracker for this boss
            var tracker = trialSystem.GetCombatTracker(config.trialKey);
            if (tracker == null || !tracker.IsFinished) return;

            string playerUid = player.PlayerUID;

            // Evaluate challenges
            var completedChallenges = TrialChallengeEvaluator.Evaluate(config, tracker, playerUid);

            // Process rewards via reputation manager
            var repManager = trialSystem.GetReputationManager();
            repManager?.ProcessBossKill(playerUid, config, completedChallenges);

            // Notify player
            if (completedChallenges.Count > 0)
            {
                string challengeList = string.Join(", ", completedChallenges);
                int bonus = TrialChallengeEvaluator.CalculateChallengeReputation(config.tier, completedChallenges.Count);
                string msg = LocalizationUtils.GetSafe("albase:trial-challenges-completed", challengeList, bonus);
                sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
            }

            // Roll quality for the reward item
            int quality = TrialQualityRoller.Roll(config.tier, completedChallenges, sapi.World.Rand);
            player.Entity.WatchedAttributes.SetInt("alegacyvsquest:trial:lastRewardQuality", quality);
            player.Entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:trial:lastRewardQuality");

            // Clean up tracker
            tracker.Reset();
        }
    }
}
