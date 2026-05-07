using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;

namespace VsQuest
{
    /// <summary>
    /// Builds QuestInfoMessage packets for sending quest information to clients.
    /// Handles reputation info, completion rewards, and rank rewards.
    /// </summary>
    public class QuestGiverMessageBuilder
    {
        private readonly ICoreServerAPI sapi;
        private readonly QuestSystem questSystem;
        private readonly QuestCompletionRewardSystem rewardSystem;
        private readonly ReputationSystem reputationSystem;

        /// <summary>
        /// Creates a new QuestGiverMessageBuilder.
        /// </summary>
        public QuestGiverMessageBuilder(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            this.questSystem = sapi?.ModLoader?.GetModSystem<QuestSystem>();
            this.rewardSystem = sapi?.ModLoader?.GetModSystem<QuestCompletionRewardSystem>();
            this.reputationSystem = sapi?.ModLoader?.GetModSystem<ReputationSystem>();
        }

        /// <summary>
        /// Creates a base QuestInfoMessage with common fields populated.
        /// </summary>
        public QuestInfoMessage CreateBaseMessage(
            long questGiverId,
            List<string> availableQuestIds,
            List<ActiveQuestDto> activeQuests,
            string noAvailableQuestDescLangKey,
            string noAvailableQuestCooldownDescLangKey,
            int cooldownDaysLeft,
            int rotationDaysLeft)
        {
            return new QuestInfoMessage
            {
                questGiverId = questGiverId,
                availableQestIds = availableQuestIds,
                activeQuests = activeQuests,
                noAvailableQuestDescLangKey = noAvailableQuestDescLangKey,
                noAvailableQuestCooldownDescLangKey = noAvailableQuestCooldownDescLangKey,
                noAvailableQuestCooldownDaysLeft = cooldownDaysLeft,
                noAvailableQuestRotationDaysLeft = rotationDaysLeft
            };
        }

        /// <summary>
        /// Populates reputation-related fields in a QuestInfoMessage.
        /// </summary>
        public void PopulateReputationInfo(
            QuestInfoMessage message,
            IServerPlayer serverPlayer,
            string reputationNpcId,
            string reputationFactionId)
        {
            if (message == null || sapi == null) return;

            message.reputationNpcId = reputationNpcId;
            message.reputationFactionId = reputationFactionId;

            if (rewardSystem != null && questSystem != null)
            {
                message.completionRewards = BuildCompletionRewardStatuses(serverPlayer, questSystem, rewardSystem, reputationNpcId, reputationFactionId);
            }

            if (serverPlayer != null && (!string.IsNullOrWhiteSpace(reputationNpcId) || !string.IsNullOrWhiteSpace(reputationFactionId)))
            {
                if (reputationSystem != null)
                {
                    if (!string.IsNullOrWhiteSpace(reputationNpcId))
                    {
                        PopulateNpcReputation(message, serverPlayer, reputationNpcId);
                    }

                    if (!string.IsNullOrWhiteSpace(reputationFactionId))
                    {
                        PopulateFactionReputation(message, serverPlayer, reputationFactionId);
                    }
                }
            }
        }

        private void PopulateNpcReputation(QuestInfoMessage message, IServerPlayer serverPlayer, string npcId)
        {
            message.reputationNpcValue = reputationSystem.GetReputationValue(serverPlayer as IPlayer, ReputationScope.Npc, npcId);
            var def = reputationSystem.GetNpcDefinition(npcId);
            message.reputationNpcRankLangKey = reputationSystem.GetRankLangKey(def, message.reputationNpcValue);
            message.reputationNpcTitleLangKey = def?.titleLangKey;
            message.reputationNpcHasRewards = reputationSystem.HasPendingRewards(serverPlayer, ReputationScope.Npc, npcId);
            message.reputationNpcRewardsCount = reputationSystem.GetPendingRewardsCount(serverPlayer, ReputationScope.Npc, npcId);
            message.reputationNpcRankRewards = BuildRankRewardStatuses(reputationSystem, serverPlayer, ReputationScope.Npc, npcId, message.reputationNpcValue);
        }

        private void PopulateFactionReputation(QuestInfoMessage message, IServerPlayer serverPlayer, string factionId)
        {
            message.reputationFactionValue = reputationSystem.GetReputationValue(serverPlayer as IPlayer, ReputationScope.Faction, factionId);
            var def = reputationSystem.GetFactionDefinition(factionId);
            message.reputationFactionRankLangKey = reputationSystem.GetRankLangKey(def, message.reputationFactionValue);
            message.reputationFactionTitleLangKey = def?.titleLangKey;
            message.reputationFactionHasRewards = reputationSystem.HasPendingRewards(serverPlayer, ReputationScope.Faction, factionId);
            message.reputationFactionRewardsCount = reputationSystem.GetPendingRewardsCount(serverPlayer, ReputationScope.Faction, factionId);
            message.reputationFactionRankRewards = BuildRankRewardStatuses(reputationSystem, serverPlayer, ReputationScope.Faction, factionId, message.reputationFactionValue);
        }

        /// <summary>
        /// Builds completion reward statuses for NPC and faction rewards.
        /// </summary>
        public List<QuestCompletionRewardStatus> BuildCompletionRewardStatuses(
            IServerPlayer serverPlayer,
            QuestSystem questSystem,
            QuestCompletionRewardSystem rewardSystem,
            string reputationNpcId,
            string reputationFactionId)
        {
            var results = new List<QuestCompletionRewardStatus>();
            if (serverPlayer == null || questSystem == null || rewardSystem == null) return results;

            if (!string.IsNullOrWhiteSpace(reputationNpcId))
            {
                var rewards = rewardSystem.GetRewardsForTarget("npc", reputationNpcId);
                AddRewardStatuses(results, rewards, serverPlayer, questSystem, rewardSystem);
            }

            if (!string.IsNullOrWhiteSpace(reputationFactionId))
            {
                var rewards = rewardSystem.GetRewardsForTarget("faction", reputationFactionId);
                AddRewardStatuses(results, rewards, serverPlayer, questSystem, rewardSystem);
            }

            return results;
        }

        private void AddRewardStatuses(
            List<QuestCompletionRewardStatus> target,
            IEnumerable<QuestCompletionReward> rewards,
            IServerPlayer serverPlayer,
            QuestSystem questSystem,
            QuestCompletionRewardSystem rewardSystem)
        {
            if (target == null || rewards == null || serverPlayer == null || questSystem == null || rewardSystem == null) return;

            var completed = new HashSet<string>(questSystem.GetNormalizedCompletedQuestIds(serverPlayer as IPlayer), StringComparer.OrdinalIgnoreCase);

            foreach (var reward in rewards)
            {
                if (reward == null || string.IsNullOrWhiteSpace(reward.id)) continue;

                bool claimed = rewardSystem.IsClaimed(serverPlayer as IPlayer, reward);
                bool eligible = !claimed && rewardSystem.RequirementsMet(serverPlayer as IPlayer, reward, questSystem);

                int remainingCount = 0;
                var remainingTitles = new List<string>();
                if (reward.requiredQuestIds != null)
                {
                    for (int i = 0; i < reward.requiredQuestIds.Count; i++)
                    {
                        var requiredId = reward.requiredQuestIds[i];
                        if (string.IsNullOrWhiteSpace(requiredId)) continue;
                        if (!completed.Contains(requiredId))
                        {
                            remainingCount++;
                            remainingTitles.Add(Lang.Get(requiredId + "-title"));
                        }
                    }
                }

                string title = string.IsNullOrWhiteSpace(reward.titleLangKey)
                    ? reward.id
                    : Lang.Get(reward.titleLangKey);

                string requirementText = string.IsNullOrWhiteSpace(reward.requirementLangKey)
                    ? string.Empty
                    : Lang.Get(reward.requirementLangKey);

                if (remainingCount > 0)
                {
                    string remainingText = Lang.Get("alegacyvsquest:reputation-remaining-template", remainingCount);
                    string list = remainingTitles.Count > 0
                        ? string.Join("\n", remainingTitles)
                        : string.Empty;
                    requirementText = string.IsNullOrWhiteSpace(list)
                        ? remainingText
                        : $"{remainingText}\n{list}";
                }
                else if (eligible && string.IsNullOrWhiteSpace(requirementText))
                {
                    requirementText = Lang.Get("alegacyvsquest:reputation-available");
                }

                target.Add(new QuestCompletionRewardStatus
                {
                    id = reward.id,
                    title = title,
                    requirementText = requirementText,
                    x = reward.x,
                    y = reward.y,
                    status = claimed ? "claimed" : (eligible ? "available" : "locked"),
                    iconItemCode = reward.iconItemCode
                });
            }
        }

        /// <summary>
        /// Builds rank reward statuses for reputation ranks.
        /// </summary>
        public List<ReputationRankRewardStatus> BuildRankRewardStatuses(
            ReputationSystem repSystem,
            IServerPlayer serverPlayer,
            ReputationScope scope,
            string id,
            int currentValue)
        {
            var results = new List<ReputationRankRewardStatus>();
            if (repSystem == null || serverPlayer?.Entity?.WatchedAttributes == null) return results;
            if (string.IsNullOrWhiteSpace(id)) return results;

            var def = scope == ReputationScope.Npc
                ? repSystem.GetNpcDefinition(id)
                : repSystem.GetFactionDefinition(id);

            if (def?.ranks == null || def.ranks.Count == 0) return results;

            var wa = serverPlayer.Entity.WatchedAttributes;

            for (int i = 0; i < def.ranks.Count; i++)
            {
                var rank = def.ranks[i];
                if (rank == null) continue;
                if (string.IsNullOrWhiteSpace(rank.rewardAction)) continue;

                string onceKey = repSystem.GetRewardOnceKeyForRank(scope, id, rank);
                bool claimed = !string.IsNullOrWhiteSpace(onceKey) && wa.GetBool(onceKey, false);
                bool meets = currentValue >= rank.min;
                string status = claimed ? "claimed" : (meets ? "available" : "locked");

                results.Add(new ReputationRankRewardStatus
                {
                    min = rank.min,
                    rankLangKey = rank.rankLangKey,
                    status = status,
                    iconItemCode = ReputationSystem.TryGetIconItemCodeFromRewardAction(rank.rewardAction)
                });
            }

            return results;
        }

        /// <summary>
        /// Sends a QuestInfoMessage to a player.
        /// </summary>
        public void SendMessage(QuestInfoMessage message, IServerPlayer player)
        {
            if (message == null || player == null || sapi == null) return;
            sapi.Network.GetChannel(VsQuestNetworkRegistry.QuestChannelName).SendPacket(message, player);
        }
    }
}
