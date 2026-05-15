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

            // Quick check: is this a trial boss? (avoid expensive FindConfig for non-trial bosses)
            if (!qt.TargetId.Contains(":trial:")) return;

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
        /// SOLO ENFORCEMENT: If more than 1 player dealt damage, kill is voided for everyone.
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

            // SOLO ENFORCEMENT: if multiple players participated, void the kill
            if (tracker.DamageByPlayer.Count > 1)
            {
                // Broadcast boss death without rewards (red text)
                string bossNameVoid = LocalizationUtils.GetSafe("albase:trial-" + ExtractBossName(cfg.trialKey) + "-title");
                string killMsgVoid = LocalizationUtils.GetSafe("albase:trial-boss-killed-chat-voided", bossNameVoid);
                GlobalChatBroadcastUtil.BroadcastGeneralChat(sapi, killMsgVoid, EnumChatType.Notification);

                tracker.Reset();
                return;
            }

            // Determine kill credit (single player)
            string creditPlayerUid = tracker.GetKillCreditPlayer(sapi);
            if (string.IsNullOrWhiteSpace(creditPlayerUid)) return;

            var creditPlayer = sapi.World.PlayerByUid(creditPlayerUid) as IServerPlayer;
            if (creditPlayer == null) return;

            // NOTE: Boss kill announcement is handled by the generic BossCombatService
            // (which also sends Discord embeds). We only handle trial-specific notifications here.

            // NOTE: Rewards (reputation, shards, challenges, quality) are processed
            // exclusively in TrialChallengeBonusesAction when the quest completes.
            // This avoids double-granting. Here we only broadcast and store quality for the action.

            // Store a preliminary quality roll (tier 1 baseline — action will override with correct tier)
            var tier1Challenges = cfg.GetChallenges(1);
            var completedChallenges = TrialChallengeEvaluator.Evaluate(tier1Challenges, tracker, creditPlayerUid);

            // Store quality roll result for quest reward action to pick up
            int quality = TrialQualityRoller.Roll(1, completedChallenges, sapi.World.Rand);
            creditPlayer.Entity.WatchedAttributes.SetInt("alegacyvsquest:trial:lastRewardQuality", quality);
            creditPlayer.Entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:trial:lastRewardQuality");
        }

        /// <summary>
        /// Called when a player dies. Records death in active trial combat trackers.
        /// Notifies about "deathless" challenge failure.
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

                    // Notify about deathless challenge failure
                    NotifyChallengeFailure(player, "deathless");
                }
            }
        }

        /// <summary>
        /// Event handler for sapi.Event.PlayerDeath.
        /// </summary>
        private void OnPlayerDeathHandler(IServerPlayer player, DamageSource damageSource)
        {
            OnPlayerDeath(player);
        }

        /// <summary>
        /// Periodic scan: for every active in-progress trial fight, record current
        /// armor tier and saturation of online players whose damage is being tracked.
        /// Armor scan lets "lowgear" challenge fail if player swaps to high-tier armor mid-fight.
        /// Saturation scan lets "nofood" challenge fail if player eats during fight.
        /// </summary>
        private void OnArmorScanTick(float dt)
        {
            if (sapi == null) return;
            if (state?.activeTrialKeys == null) return;

            foreach (var trialKey in state.activeTrialKeys)
            {
                var tracker = GetCombatTracker(trialKey);
                if (tracker == null || !tracker.IsStarted || tracker.IsFinished) continue;

                // For each player who has dealt damage to this boss, sample armor tier and saturation
                foreach (var kvp in tracker.DamageByPlayer)
                {
                    var player = sapi.World.PlayerByUid(kvp.Key) as IServerPlayer;
                    if (player == null || player.ConnectionState != EnumClientState.Playing) continue;

                    string playerUid = player.PlayerUID;

                    // Armor tier tracking (with lowgear failure notification)
                    int armorTier = TrialArmorTierUtils.GetMaxArmorTier(player);
                    int prevMaxTier = 0;
                    tracker.MaxArmorTierByPlayer.TryGetValue(playerUid, out prevMaxTier);
                    tracker.RecordArmorTier(playerUid, armorTier);

                    // Notify if armor tier just exceeded a common threshold (tier 3 = plate)
                    if (armorTier > prevMaxTier && armorTier >= 3 && prevMaxTier < 3)
                    {
                        NotifyChallengeFailure(player, "lowgear");
                    }

                    // Saturation tracking for "nofood" challenge
                    float currentSaturation = player.Entity?.WatchedAttributes
                        ?.GetTreeAttribute("hunger")?.GetFloat("currentsaturation", 0) ?? 0;

                    if (!tracker.InitialSaturationByPlayer.ContainsKey(playerUid))
                    {
                        // Record initial (max) saturation on first scan
                        tracker.RecordInitialSaturation(playerUid, currentSaturation);
                    }
                    else
                    {
                        // Check if saturation increased above the recorded max
                        // (natural decay means saturation drops; eating raises it)
                        float maxRecorded = tracker.InitialSaturationByPlayer[playerUid];

                        if (currentSaturation > maxRecorded + 0.5f) // player ate food
                        {
                            if (!tracker.SaturationGainedByPlayer.ContainsKey(playerUid) ||
                                !tracker.SaturationGainedByPlayer[playerUid])
                            {
                                // First time detecting food — notify
                                NotifyChallengeFailure(player, "nofood");
                            }
                            tracker.RecordSaturationGain(playerUid);
                            // Update max to current so we detect further eating
                            tracker.InitialSaturationByPlayer[playerUid] = currentSaturation;
                        }
                        else if (currentSaturation > maxRecorded)
                        {
                            // Small increase within threshold — update max silently
                            tracker.InitialSaturationByPlayer[playerUid] = currentSaturation;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Send a chat notification to a player when a challenge is failed in real-time.
        /// </summary>
        private void NotifyChallengeFailure(IServerPlayer player, string challengeType)
        {
            if (sapi == null || player == null) return;

            string msgKey = $"albase:trial-challenge-failed-{challengeType}";
            string msg = LocalizationUtils.GetSafe(msgKey);
            if (!string.IsNullOrWhiteSpace(msg))
            {
                sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
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
