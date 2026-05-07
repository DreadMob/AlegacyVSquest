using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public enum ReputationScope
    {
        Npc,
        Faction
    }

    public class ReputationSystem : ModSystem
    {
        public const string DefaultDefinitionId = "alegacyvsquest:default";
        private const string NpcKeyPrefix = "alegacyvsquest:rep:npc:";
        private const string FactionKeyPrefix = "alegacyvsquest:rep:faction:";
        private const string RewardKeyPrefix = "alegacyvsquest:rep:reward:";

        private readonly Dictionary<string, ReputationDefinition> factionDefinitions = new Dictionary<string, ReputationDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ReputationDefinition> npcDefinitions = new Dictionary<string, ReputationDefinition>(StringComparer.OrdinalIgnoreCase);

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);
            LoadConfigs(api);
        }

        private void LoadConfigs(ICoreAPI api)
        {
            factionDefinitions.Clear();
            npcDefinitions.Clear();

            foreach (var mod in api.ModLoader.Mods)
            {
                var assets = api.Assets.GetMany<ReputationConfig>(api.Logger, "config/reputation", mod.Info.ModID);
                foreach (var asset in assets)
                {
                    var config = asset.Value;
                    if (config == null) continue;

                    MergeConfig(config);
                }
            }
        }

        private string GetRewardOnceKey(ReputationScope scope, string id, ReputationRank rank)
        {
            if (rank == null) return null;

            string onceKey = rank.rewardOnceKey;
            if (string.IsNullOrWhiteSpace(onceKey))
            {
                onceKey = RewardKeyPrefix + scope.ToString().ToLowerInvariant() + ":" + id + ":" + (rank.rankLangKey ?? rank.min.ToString());
            }

            return onceKey;
        }

        public string GetRewardOnceKeyForRank(ReputationScope scope, string id, ReputationRank rank)
        {
            return GetRewardOnceKey(scope, id, rank);
        }

        public static string TryGetIconItemCodeFromRewardAction(string rewardAction)
        {
            if (string.IsNullOrWhiteSpace(rewardAction)) return null;
            // Expected: "giveitem <code> <amount>" or "questitem <actionItemId>".
            var parts = rewardAction.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return null;

            string action = parts[0];
            if (string.Equals(action, "giveitem", StringComparison.OrdinalIgnoreCase))
            {
                return parts[1];
            }

            if (string.Equals(action, "questitem", StringComparison.OrdinalIgnoreCase) || string.Equals(action, "giveactionitem", StringComparison.OrdinalIgnoreCase))
            {
                return parts[1];
            }

            return null;
        }

        private void MergeConfig(ReputationConfig config)
        {
            if (config?.factions != null)
            {
                foreach (var entry in config.factions)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key)) continue;
                    factionDefinitions[entry.Key] = NormalizeDefinition(entry.Value);
                }
            }

            if (config?.npcs != null)
            {
                foreach (var entry in config.npcs)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key)) continue;
                    npcDefinitions[entry.Key] = NormalizeDefinition(entry.Value);
                }
            }
        }

        private ReputationDefinition NormalizeDefinition(ReputationDefinition definition)
        {
            if (definition == null) return null;

            definition.ranks ??= new List<ReputationRank>();
            definition.ranks = definition.ranks
                .Where(rank => rank != null)
                .OrderBy(rank => rank.min)
                .ToList();

            return definition;
        }

        public ReputationDefinition GetFactionDefinition(string id)
        {
            return GetDefinition(factionDefinitions, id);
        }

        public ReputationDefinition GetNpcDefinition(string id)
        {
            return GetDefinition(npcDefinitions, id);
        }

        public IEnumerable<string> GetAllNpcIds()
        {
            return npcDefinitions.Keys;
        }

        public IEnumerable<string> GetAllFactionIds()
        {
            return factionDefinitions.Keys;
        }

        public IEnumerable<string> GetRankRewardOnceKeys(ReputationScope scope, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) yield break;

            var definition = scope == ReputationScope.Npc
                ? GetNpcDefinition(id)
                : GetFactionDefinition(id);

            if (definition?.ranks == null) yield break;

            for (int i = 0; i < definition.ranks.Count; i++)
            {
                var rank = definition.ranks[i];
                if (rank == null) continue;
                if (string.IsNullOrWhiteSpace(rank.rewardAction)) continue;

                string onceKey = GetRewardOnceKey(scope, id, rank);
                if (!string.IsNullOrWhiteSpace(onceKey))
                {
                    yield return onceKey;
                }
            }
        }

        private ReputationDefinition GetDefinition(Dictionary<string, ReputationDefinition> definitions, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            if (definitions.TryGetValue(id, out var definition)) return definition;

            if (definitions.TryGetValue(DefaultDefinitionId, out var fallback)) return fallback;

            return null;
        }

        public string GetRankLangKey(ReputationDefinition definition, int value)
        {
            if (definition?.ranks == null || definition.ranks.Count == 0) return null;

            ReputationRank best = null;
            for (int i = 0; i < definition.ranks.Count; i++)
            {
                var rank = definition.ranks[i];
                if (rank == null) continue;
                if (rank.min <= value)
                {
                    best = rank;
                }
            }

            return best?.rankLangKey;
        }

        public bool TryParseScope(string scopeRaw, out ReputationScope scope)
        {
            scope = ReputationScope.Npc;
            if (string.IsNullOrWhiteSpace(scopeRaw)) return false;

            if (scopeRaw == "npc")
            {
                scope = ReputationScope.Npc;
                return true;
            }

            if (scopeRaw == "faction")
            {
                scope = ReputationScope.Faction;
                return true;
            }

            return false;
        }

        public int GetPendingRewardsCount(IServerPlayer player, ReputationScope scope, string id)
        {
            if (player?.Entity?.WatchedAttributes == null) return 0;
            if (string.IsNullOrWhiteSpace(id)) return 0;

            var definition = scope == ReputationScope.Npc
                ? GetNpcDefinition(id)
                : GetFactionDefinition(id);

            if (definition?.ranks == null || definition.ranks.Count == 0) return 0;

            int value = GetReputationValue(player as IPlayer, scope, id);
            var wa = player.Entity.WatchedAttributes;
            int pending = 0;

            for (int i = 0; i < definition.ranks.Count; i++)
            {
                var rank = definition.ranks[i];
                if (rank == null) continue;
                if (string.IsNullOrWhiteSpace(rank.rewardAction)) continue;
                if (value < rank.min) continue;

                string onceKey = GetRewardOnceKey(scope, id, rank);
                if (!string.IsNullOrWhiteSpace(onceKey) && wa.GetBool(onceKey, false))
                {
                    continue;
                }

                pending++;
            }

            return pending;
        }

        public bool MeetsRequirement(IPlayer player, QuestReputationRequirement requirement)
        {
            if (player == null || requirement == null) return false;

            int required = requirement.minValue;

            if (!string.IsNullOrWhiteSpace(requirement.rankLangKey))
            {
                if (!TryParseScope(requirement.scope, out var scope)) return false;
                string id = requirement.id;
                if (string.IsNullOrWhiteSpace(id)) return false;

                var definition = scope == ReputationScope.Npc
                    ? GetNpcDefinition(id)
                    : GetFactionDefinition(id);

                if (definition?.ranks != null)
                {
                    for (int i = 0; i < definition.ranks.Count; i++)
                    {
                        var rank = definition.ranks[i];
                        if (rank == null) continue;
                        if (string.Equals(rank.rankLangKey, requirement.rankLangKey, StringComparison.OrdinalIgnoreCase))
                        {
                            if (rank.min > required) required = rank.min;
                            break;
                        }
                    }
                }
            }

            if (!TryParseScope(requirement.scope, out var checkScope)) return false;
            int current = GetReputationValue(player, checkScope, requirement.id);
            return current >= required;
        }

        public int GetReputationValue(IPlayer player, ReputationScope scope, string id)
        {
            if (player?.Entity?.WatchedAttributes == null) return 0;
            if (string.IsNullOrWhiteSpace(id)) return 0;

            string key = BuildKey(scope, id);
            return player.Entity.WatchedAttributes.GetInt(key, 0);
        }

        public int ApplyReputationChange(ICoreServerAPI sapi, IServerPlayer player, ReputationScope scope, string id, int newValue, bool grantRewards)
        {
            if (player?.Entity?.WatchedAttributes == null) return newValue;
            if (string.IsNullOrWhiteSpace(id)) return newValue;

            int oldValue = GetReputationValue(player as IPlayer, scope, id);

            SetReputationValue(player as IPlayer, scope, id, newValue);

            if (grantRewards && newValue > oldValue)
            {
                GrantRankRewards(sapi, player, scope, id, oldValue, newValue);
            }

            return newValue;
        }

        public bool HasPendingRewards(IServerPlayer player, ReputationScope scope, string id)
        {
            if (player?.Entity?.WatchedAttributes == null) return false;
            if (string.IsNullOrWhiteSpace(id)) return false;

            var definition = scope == ReputationScope.Npc
                ? GetNpcDefinition(id)
                : GetFactionDefinition(id);

            if (definition?.ranks == null || definition.ranks.Count == 0) return false;

            int value = GetReputationValue(player as IPlayer, scope, id);
            var wa = player.Entity.WatchedAttributes;

            for (int i = 0; i < definition.ranks.Count; i++)
            {
                var rank = definition.ranks[i];
                if (rank == null) continue;
                if (string.IsNullOrWhiteSpace(rank.rewardAction)) continue;
                if (value < rank.min) continue;

                string onceKey = GetRewardOnceKey(scope, id, rank);
                if (!string.IsNullOrWhiteSpace(onceKey) && wa.GetBool(onceKey, false))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        public int ClaimPendingRewards(ICoreServerAPI sapi, IServerPlayer player, ReputationScope scope, string id)
        {
            if (sapi == null || player?.Entity?.WatchedAttributes == null) return 0;
            if (string.IsNullOrWhiteSpace(id)) return 0;

            var definition = scope == ReputationScope.Npc
                ? GetNpcDefinition(id)
                : GetFactionDefinition(id);

            if (definition?.ranks == null || definition.ranks.Count == 0) return 0;

            int value = GetReputationValue(player as IPlayer, scope, id);
            var wa = player.Entity.WatchedAttributes;
            int claimed = 0;

            for (int i = 0; i < definition.ranks.Count; i++)
            {
                var rank = definition.ranks[i];
                if (rank == null) continue;
                if (string.IsNullOrWhiteSpace(rank.rewardAction)) continue;
                if (value < rank.min) continue;

                string onceKey = GetRewardOnceKey(scope, id, rank);
                if (!string.IsNullOrWhiteSpace(onceKey) && wa.GetBool(onceKey, false))
                {
                    continue;
                }

                var message = new QuestAcceptedMessage
                {
                    questGiverId = 0,
                    questId = "reputation-reward"
                };

                ActionStringExecutor.Execute(sapi, message, player, rank.rewardAction);
                claimed++;

                if (!string.IsNullOrWhiteSpace(onceKey))
                {
                    wa.SetBool(onceKey, true);
                    wa.MarkPathDirty(onceKey);
                }
            }

            return claimed;
        }

        public void OnClaimReputationRewardsMessage(IServerPlayer player, ClaimReputationRewardsMessage message, ICoreServerAPI sapi)
        {
            if (sapi == null || player == null || message == null) return;

            if (!TryResolveQuestGiverReputation(sapi, message.questGiverId, out string repNpcId, out string repFactionId))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(message.scope) || string.Equals(message.scope, "npc", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(repNpcId))
                {
                    ClaimPendingRewards(sapi, player, ReputationScope.Npc, repNpcId);
                }
            }

            if (string.IsNullOrWhiteSpace(message.scope) || string.Equals(message.scope, "faction", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(repFactionId))
                {
                    ClaimPendingRewards(sapi, player, ReputationScope.Faction, repFactionId);
                }
            }

            var questGiver = sapi.World.GetEntityById(message.questGiverId);
            var questGiverBehavior = questGiver?.GetBehavior<EntityBehaviorQuestGiver>();
            if (questGiverBehavior != null && player.Entity is EntityPlayer entityPlayer)
            {
                questGiverBehavior.SendQuestInfoMessageToClient(sapi, entityPlayer);
            }
        }

        public void SetReputationValue(IPlayer player, ReputationScope scope, string id, int value)
        {
            if (player?.Entity?.WatchedAttributes == null) return;
            if (string.IsNullOrWhiteSpace(id)) return;

            string key = BuildKey(scope, id);
            player.Entity.WatchedAttributes.SetInt(key, value);
            player.Entity.WatchedAttributes.MarkPathDirty(key);
        }

        private void GrantRankRewards(ICoreServerAPI sapi, IServerPlayer player, ReputationScope scope, string id, int oldValue, int newValue)
        {
            if (sapi == null || player == null) return;

            var definition = scope == ReputationScope.Npc
                ? GetNpcDefinition(id)
                : GetFactionDefinition(id);

            if (definition?.ranks == null || definition.ranks.Count == 0) return;

            for (int i = 0; i < definition.ranks.Count; i++)
            {
                var rank = definition.ranks[i];
                if (rank == null) continue;

                if (newValue >= rank.min && oldValue < rank.min)
                {
                    string rewardAction = rank.rewardAction;
                    if (string.IsNullOrWhiteSpace(rewardAction))
                    {
                        continue;
                    }

                    string onceKey = GetRewardOnceKey(scope, id, rank);

                    var wa = player.Entity.WatchedAttributes;
                    if (!string.IsNullOrWhiteSpace(onceKey) && wa.GetBool(onceKey, false))
                    {
                        continue;
                    }

                    var message = new QuestAcceptedMessage
                    {
                        questGiverId = 0,
                        questId = "reputation-reward"
                    };

                    ActionStringExecutor.Execute(sapi, message, player, rewardAction);

                    if (!string.IsNullOrWhiteSpace(onceKey))
                    {
                        wa.SetBool(onceKey, true);
                        wa.MarkPathDirty(onceKey);
                    }
                }
            }
        }

        public static string BuildKey(ReputationScope scope, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            return scope == ReputationScope.Npc ? NpcKeyPrefix + id : FactionKeyPrefix + id;
        }

        public bool TryResolveQuestGiverReputation(ICoreServerAPI sapi, long questGiverId, out string npcId, out string factionId)
        {
            npcId = null;
            factionId = null;

            if (sapi == null) return false;

            var entity = sapi.World.GetEntityById(questGiverId);
            if (entity == null) return false;

            var behavior = entity.GetBehavior<EntityBehaviorQuestGiver>();
            if (behavior != null)
            {
                npcId = behavior.ReputationNpcId;
                factionId = behavior.ReputationFactionId;
            }

            if (string.IsNullOrWhiteSpace(npcId))
            {
                npcId = entity.Code?.ToString();
            }

            return !string.IsNullOrWhiteSpace(npcId) || !string.IsNullOrWhiteSpace(factionId);
        }
    }
}
