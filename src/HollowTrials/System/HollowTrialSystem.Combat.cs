using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Handles combat event integration: connects entity damage/death events
    /// to TrialCombatTracker for challenge evaluation and kill credit.
    /// </summary>
    public partial class HollowTrialSystem
    {
        /// <summary>
        /// Called when any entity receives damage. Hooks into the boss damage tracking.
        /// Must be called from a Harmony patch or entity behavior that intercepts damage.
        /// </summary>
        public void OnTrialBossDamaged(Entity bossEntity, DamageSource damageSource, float damage)
        {
            if (sapi == null || bossEntity == null || damage <= 0) return;

            var qt = bossEntity.GetBehavior<EntityBehaviorQuestTarget>();
            if (qt == null || string.IsNullOrWhiteSpace(qt.TargetId)) return;

            // Check if this is a trial boss
            var cfg = FindConfig(qt.TargetId);
            if (cfg == null) return;

            // Get the player who dealt damage
            string playerUid = GetDamageSourcePlayerUid(damageSource);
            if (string.IsNullOrWhiteSpace(playerUid)) return;

            double nowHours = sapi.World.Calendar.TotalHours;

            // Record in combat tracker
            var tracker = GetCombatTracker(cfg.trialKey);
            tracker.RecordDamage(playerUid, damage, nowHours);

            // Notify enrage behavior of damage (for timer start)
            var enrage = bossEntity.GetBehavior<EntityBehaviorBossEnrage>();
            // Enrage handles its own first-damage detection via OnEntityReceiveDamage

            // Notify curse stack behavior
            var curseStack = bossEntity.GetBehavior<EntityBehaviorBossCurseStack>();
            if (curseStack != null && damageSource?.SourceEntity is EntityPlayer)
            {
                // Curse stacks are applied when boss deals damage TO player, not when boss receives damage
                // So we don't call it here
            }
        }

        /// <summary>
        /// Called when a trial boss dies. Finalizes combat tracker and processes rewards.
        /// This extends the OnEntityDeath handler in HollowTrialSystem.Spawn.cs
        /// </summary>
        public void OnTrialBossKilled(Entity bossEntity, DamageSource damageSource)
        {
            if (sapi == null || bossEntity == null) return;

            var qt = bossEntity.GetBehavior<EntityBehaviorQuestTarget>();
            if (qt == null || string.IsNullOrWhiteSpace(qt.TargetId)) return;

            var cfg = FindConfig(qt.TargetId);
            if (cfg == null) return;

            double nowHours = sapi.World.Calendar.TotalHours;

            // Finalize combat tracker
            var tracker = GetCombatTracker(cfg.trialKey);
            tracker.MarkFinished(nowHours);

            // Determine kill credit
            string creditPlayerUid = tracker.GetKillCreditPlayer(sapi);
            if (string.IsNullOrWhiteSpace(creditPlayerUid)) return;

            var creditPlayer = sapi.World.PlayerByUid(creditPlayerUid) as IServerPlayer;
            if (creditPlayer == null) return;

            // Evaluate challenges
            var completedChallenges = TrialChallengeEvaluator.Evaluate(cfg, tracker, creditPlayerUid);

            // Process reputation + shards
            var repManager = GetReputationManager();
            repManager?.ProcessBossKill(creditPlayerUid, cfg, completedChallenges);

            // Broadcast kill
            string bossName = LocalizationUtils.GetSafe("albase:trial-" + ExtractBossName(cfg.trialKey) + "-title");
            string killMsg = LocalizationUtils.GetSafe("albase:trial-boss-killed-chat", creditPlayer.PlayerName, bossName);
            GlobalChatBroadcastUtil.BroadcastGeneralChat(sapi, killMsg, EnumChatType.Notification);

            // First kill bonus notification
            if (!repManager.HasKilledBefore(creditPlayerUid, cfg.trialKey))
            {
                // HasKilledBefore was already called in ProcessBossKill which records it,
                // but ProcessBossKill checks before recording, so this is the first time
                string firstKillMsg = LocalizationUtils.GetSafe("albase:trial-first-kill-bonus");
                sapi.SendMessage(creditPlayer, GlobalConstants.GeneralChatGroup, firstKillMsg, EnumChatType.Notification);
            }

            // Challenge completion notification
            if (completedChallenges.Count > 0)
            {
                int bonus = TrialChallengeEvaluator.CalculateChallengeReputation(cfg.tier, completedChallenges.Count);
                string challengeMsg = LocalizationUtils.GetSafe("albase:trial-challenges-completed",
                    string.Join(", ", completedChallenges), bonus);
                sapi.SendMessage(creditPlayer, GlobalConstants.GeneralChatGroup, challengeMsg, EnumChatType.Notification);
            }

            // Store quality roll result for quest reward action to pick up
            int quality = TrialQualityRoller.Roll(cfg.tier, completedChallenges, sapi.World.Rand);
            creditPlayer.Entity.WatchedAttributes.SetInt("alegacyvsquest:trial:lastRewardQuality", quality);
            creditPlayer.Entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:trial:lastRewardQuality");
        }

        /// <summary>
        /// Called when a player dies. Records death in active trial combat trackers.
        /// </summary>
        public void OnPlayerDeath(IServerPlayer player)
        {
            if (player == null || state?.activeTrialKeys == null) return;

            string playerUid = player.PlayerUID;

            foreach (var trialKey in state.activeTrialKeys)
            {
                var tracker = GetCombatTracker(trialKey);
                if (tracker.IsStarted && !tracker.IsFinished)
                {
                    tracker.RecordPlayerDeath(playerUid);
                }
            }
        }

        private string GetDamageSourcePlayerUid(DamageSource damageSource)
        {
            if (damageSource == null) return null;

            // Direct player damage
            if (damageSource.SourceEntity is EntityPlayer playerEntity)
            {
                return playerEntity.PlayerUID;
            }

            // Projectile from player
            if (damageSource.CauseEntity is EntityPlayer causePlayer)
            {
                return causePlayer.PlayerUID;
            }

            return null;
        }

        private string ExtractBossName(string trialKey)
        {
            // "albase:trial:shadow-stalker" -> "shadow-stalker"
            if (string.IsNullOrWhiteSpace(trialKey)) return "";
            var parts = trialKey.Split(':');
            return parts.Length >= 3 ? parts[2] : parts[parts.Length - 1];
        }
    }
}
