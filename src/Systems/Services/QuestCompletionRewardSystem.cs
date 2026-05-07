using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestCompletionRewardSystem : ModSystem
    {
        private const string RewardKeyPrefix = "alegacyvsquest:questcompletionreward:";
        private readonly List<QuestCompletionReward> rewards = new();

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);
            LoadConfigs(api);
        }

        private void LoadConfigs(ICoreAPI api)
        {
            rewards.Clear();

            foreach (var mod in api.ModLoader.Mods)
            {
                var assets = api.Assets.GetMany<QuestCompletionRewardConfig>(api.Logger, "config/questcompletionrewards", mod.Info.ModID);
                foreach (var asset in assets)
                {
                    var config = asset.Value;
                    if (config?.rewards == null) continue;
                    foreach (var reward in config.rewards)
                    {
                        if (reward != null)
                        {
                            rewards.Add(reward);
                        }
                    }
                }
            }
        }

        public IEnumerable<QuestCompletionReward> GetRewardsForTarget(string scope, string targetId)
        {
            if (string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(targetId)) return Array.Empty<QuestCompletionReward>();
            return rewards.Where(r => r != null
                && string.Equals(r.scope, scope, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.targetId, targetId, StringComparison.OrdinalIgnoreCase));
        }

        public QuestCompletionReward GetRewardById(string rewardId)
        {
            if (string.IsNullOrWhiteSpace(rewardId)) return null;
            return rewards.FirstOrDefault(r => r != null && string.Equals(r.id, rewardId, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<QuestCompletionReward> GetAllRewards()
        {
            return rewards;
        }

        public bool IsClaimed(IPlayer player, QuestCompletionReward reward)
        {
            if (player?.Entity?.WatchedAttributes == null || reward == null) return false;
            string key = BuildOnceKey(reward);
            return !string.IsNullOrWhiteSpace(key) && player.Entity.WatchedAttributes.GetBool(key, false);
        }

        public bool RequirementsMet(IPlayer player, QuestCompletionReward reward, QuestSystem questSystem)
        {
            if (player == null || reward == null || questSystem == null) return false;
            var required = reward.requiredQuestIds ?? new List<string>();
            if (required.Count == 0) return false;

            var completed = new HashSet<string>(questSystem.GetNormalizedCompletedQuestIds(player), StringComparer.OrdinalIgnoreCase);
            return required.All(id => completed.Contains(id));
        }

        public bool TryGrantReward(IServerPlayer player, QuestCompletionReward reward, QuestSystem questSystem, ICoreServerAPI sapi)
        {
            if (player == null || reward == null || questSystem == null || sapi == null) return false;
            if (!RequirementsMet(player as IPlayer, reward, questSystem)) return false;

            string key = BuildOnceKey(reward);
            var wa = player.Entity?.WatchedAttributes;
            if (wa == null) return false;

            if (!string.IsNullOrWhiteSpace(key) && wa.GetBool(key, false))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(reward.rewardAction))
            {
                var message = new QuestAcceptedMessage
                {
                    questGiverId = 0,
                    questId = string.IsNullOrWhiteSpace(reward.id) ? "questcompletionreward" : reward.id
                };

                ActionStringExecutor.Execute(sapi, message, player, reward.rewardAction);
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                wa.SetBool(key, true);
                wa.MarkPathDirty(key);
            }

            return true;
        }

        public void OnClaimQuestCompletionRewardMessage(IServerPlayer player, ClaimQuestCompletionRewardMessage message, ICoreServerAPI sapi)
        {
            if (sapi == null || player == null || message == null) return;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return;

            var reward = GetRewardById(message.rewardId);
            if (reward == null) return;

            TryGrantReward(player, reward, questSystem, sapi);

            var questGiver = sapi.World.GetEntityById(message.questGiverId);
            var questGiverBehavior = questGiver?.GetBehavior<EntityBehaviorQuestGiver>();
            if (questGiverBehavior != null && player.Entity is EntityPlayer entityPlayer)
            {
                questGiverBehavior.SendQuestInfoMessageToClient(sapi, entityPlayer);
            }
        }

        public string BuildOnceKey(QuestCompletionReward reward)
        {
            if (reward == null) return null;
            if (!string.IsNullOrWhiteSpace(reward.onceKey)) return reward.onceKey;
            if (string.IsNullOrWhiteSpace(reward.id)) return null;

            return RewardKeyPrefix + reward.id;
        }
    }
}
