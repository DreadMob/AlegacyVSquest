using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Checks quest eligibility including cooldowns, predecessors, and reputation requirements.
    /// </summary>
    public class QuestEligibilityChecker
    {
        private readonly ICoreServerAPI sapi;
        private readonly QuestSystem questSystem;
        private readonly ReputationSystem reputationSystem;

        /// <summary>
        /// Creates a new QuestEligibilityChecker.
        /// </summary>
        public QuestEligibilityChecker(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            this.questSystem = sapi?.ModLoader?.GetModSystem<QuestSystem>();
            this.reputationSystem = sapi?.ModLoader?.GetModSystem<ReputationSystem>();
        }

        /// <summary>
        /// Checks if a player meets all eligibility requirements for a quest.
        /// </summary>
        /// <param name="quest">The quest to check</param>
        /// <param name="player">The player to check</param>
        /// <param name="ignorePredecessors">If true, skip predecessor checks</param>
        /// <param name="activeQuestIds">Set of quest IDs the player currently has active</param>
        /// <param name="completedQuestIds">Set of quest IDs the player has completed</param>
        /// <returns>Tuple of (isEligible, isOnCooldown, cooldownDaysLeft)</returns>
        public (bool isEligible, bool isOnCooldown, int cooldownDaysLeft) CheckEligibility(
            Quest quest,
            IServerPlayer player,
            bool ignorePredecessors,
            HashSet<string> activeQuestIds,
            HashSet<string> completedQuestIds)
        {
            if (quest == null || player?.Entity?.WatchedAttributes == null)
            {
                return (false, false, 0);
            }

            string questId = quest.id;

            // Check if already active
            bool isActive = activeQuestIds.Contains(questId);
            if (isActive)
            {
                return (false, false, 0);
            }

            // Check one-time quest (cooldown < 0 means never offer again after completion)
            bool oneTimeBlocked = quest.cooldown < 0 && completedQuestIds.Contains(questId);
            if (oneTimeBlocked)
            {
                return (false, false, 0);
            }

            // Check predecessors
            bool predecessorsMet = ignorePredecessors || PredecessorsCompleted(quest, player.PlayerUID, completedQuestIds);
            if (!predecessorsMet)
            {
                return (false, false, 0);
            }

            // Check reputation requirements
            bool meetsReputation = MeetsReputationRequirements(quest, player.PlayerUID);
            if (!meetsReputation)
            {
                return (false, false, 0);
            }

            // Check cooldown
            var cooldownResult = CheckCooldown(quest, player);
            if (cooldownResult.isOnCooldown)
            {
                return (false, true, cooldownResult.daysLeft);
            }

            return (true, false, 0);
        }

        /// <summary>
        /// Checks if a quest is on cooldown for a player.
        /// </summary>
        public (bool isOnCooldown, int daysLeft) CheckCooldown(Quest quest, IServerPlayer player)
        {
            if (quest == null || player?.Entity?.WatchedAttributes == null)
            {
                return (false, 0);
            }

            if (quest.cooldown <= 0)
            {
                return (false, 0);
            }

            string key = QuestGiverConstants.LastAcceptedKey(quest.id);
            double lastAccepted = player.Entity.WatchedAttributes.GetDouble(key, double.NaN);

            if (double.IsNaN(lastAccepted))
            {
                lastAccepted = -quest.cooldown;
            }

            double nowDays = sapi.World.Calendar.TotalDays;

            // Handle time rewind (e.g., testing)
            if (!double.IsNaN(lastAccepted) && !double.IsInfinity(lastAccepted) && nowDays + 0.0001 < lastAccepted)
            {
                lastAccepted = nowDays - Math.Max(0, quest.cooldown) - 0.0001;
                player.Entity.WatchedAttributes.SetDouble(key, lastAccepted);
                player.Entity.WatchedAttributes.MarkPathDirty(key);
            }

            bool onCooldown = lastAccepted + quest.cooldown >= nowDays;
            if (!onCooldown)
            {
                return (false, 0);
            }

            double daysLeft = (lastAccepted + quest.cooldown) - nowDays;
            if (double.IsNaN(daysLeft) || double.IsInfinity(daysLeft))
            {
                daysLeft = 0;
            }

            int left = (int)Math.Ceiling(daysLeft);
            left = Math.Max(0, left);

            // Safety clamp
            if (quest.cooldown >= 0 && left > quest.cooldown)
            {
                left = quest.cooldown;
            }

            return (true, left);
        }

        /// <summary>
        /// Checks if a player meets reputation requirements for a quest.
        /// </summary>
        public bool MeetsReputationRequirements(Quest quest, string playerUID)
        {
            if (quest?.reputationRequirements == null || quest.reputationRequirements.Count == 0)
            {
                return true;
            }

            if (reputationSystem == null) return false;

            var player = sapi.World.PlayerByUid(playerUID);
            if (player == null) return false;

            for (int i = 0; i < quest.reputationRequirements.Count; i++)
            {
                var req = quest.reputationRequirements[i];
                if (req == null) continue;
                if (!reputationSystem.MeetsRequirement(player, req)) return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if all predecessors for a quest have been completed.
        /// </summary>
        public bool PredecessorsCompleted(Quest quest, string playerUID, HashSet<string> completedQuestIds)
        {
            // Legacy: single predecessor
            if (!string.IsNullOrEmpty(quest.predecessor))
            {
                string predecessor = questSystem?.NormalizeQuestId(quest.predecessor) ?? quest.predecessor;
                if (!completedQuestIds.Contains(predecessor))
                {
                    return false;
                }
            }

            // New: list of predecessors (all must be completed)
            if (quest.predecessors != null)
            {
                for (int i = 0; i < quest.predecessors.Count; i++)
                {
                    string pred = quest.predecessors[i];
                    if (string.IsNullOrWhiteSpace(pred)) continue;

                    if (questSystem != null)
                    {
                        pred = questSystem.NormalizeQuestId(pred);
                    }

                    if (!completedQuestIds.Contains(pred))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Checks chain cooldown (global cooldown after completing any quest from a quest giver).
        /// </summary>
        public (bool isOnCooldown, int daysLeft) CheckChainCooldown(long questGiverEntityId, int chainCooldownDays, IServerPlayer player)
        {
            if (chainCooldownDays <= 0 || player?.Entity?.WatchedAttributes == null)
            {
                return (false, 0);
            }

            double nowDays = sapi.World.Calendar.TotalDays;
            string chainKey = QuestGiverConstants.ChainCooldownKey(questGiverEntityId);
            double lastCompleted = player.Entity.WatchedAttributes.GetDouble(chainKey, double.NaN);

            if (double.IsNaN(lastCompleted) || double.IsInfinity(lastCompleted))
            {
                return (false, 0);
            }

            // Handle time rewind
            if (nowDays + 0.0001 < lastCompleted)
            {
                lastCompleted = nowDays - chainCooldownDays - 0.0001;
                player.Entity.WatchedAttributes.SetDouble(chainKey, lastCompleted);
                player.Entity.WatchedAttributes.MarkPathDirty(chainKey);
            }

            double leftDays = (lastCompleted + chainCooldownDays) - nowDays;
            if (double.IsNaN(leftDays) || double.IsInfinity(leftDays) || leftDays <= 0)
            {
                return (false, 0);
            }

            int left = (int)Math.Ceiling(leftDays);
            left = Math.Max(0, Math.Min(left, chainCooldownDays));

            return (true, left);
        }

        /// <summary>
        /// Gets the set of completed quest IDs for a player.
        /// </summary>
        public HashSet<string> GetCompletedQuestIds(IPlayer player)
        {
            if (questSystem != null)
            {
                return new HashSet<string>(questSystem.GetNormalizedCompletedQuestIds(player), StringComparer.OrdinalIgnoreCase);
            }

            // Fallback to legacy attribute
            var completed = player?.Entity?.WatchedAttributes?.GetStringArray(
                QuestGiverConstants.PlayerCompletedQuestsKey,
                Array.Empty<string>());
            return new HashSet<string>(completed ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
