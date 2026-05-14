using System;
using System.Collections.Generic;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Manages per-player trial reputation (never spent) and Void Shards (currency, spent in shop).
    /// Data stored in world save via HollowTrialSystem state.
    /// </summary>
    public class TrialReputationManager
    {
        private const string SaveKey = "alegacyvsquest:hollowtrials:playerdata";

        private readonly ICoreServerAPI sapi;
        private Dictionary<string, TrialPlayerData> playerData = new(StringComparer.OrdinalIgnoreCase);
        private bool dirty;

        public TrialReputationManager(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            Load();
        }

        /// <summary>
        /// Get player's current reputation (never decreases).
        /// </summary>
        public int GetReputation(string playerUid)
        {
            return GetOrCreate(playerUid).reputation;
        }

        /// <summary>
        /// Get player's current Void Shard balance (currency).
        /// </summary>
        public int GetVoidShards(string playerUid)
        {
            return GetOrCreate(playerUid).voidShards;
        }

        /// <summary>
        /// Add reputation and void shards simultaneously (both increase on boss kill).
        /// </summary>
        public void AddReputationAndShards(string playerUid, int amount)
        {
            if (amount <= 0) return;
            var data = GetOrCreate(playerUid);
            data.reputation += amount;
            data.voidShards += amount;
            dirty = true;
        }

        /// <summary>
        /// Spend void shards (for shop purchases). Returns false if insufficient.
        /// </summary>
        public bool SpendVoidShards(string playerUid, int amount)
        {
            if (amount <= 0) return true;
            var data = GetOrCreate(playerUid);
            if (data.voidShards < amount) return false;

            data.voidShards -= amount;
            dirty = true;
            return true;
        }

        /// <summary>
        /// Check if player has killed a specific trial boss before (for first-kill bonus).
        /// </summary>
        public bool HasKilledBefore(string playerUid, string trialKey)
        {
            var data = GetOrCreate(playerUid);
            return data.killedTrialKeys.Contains(trialKey);
        }

        /// <summary>
        /// Record a first kill for a trial boss.
        /// </summary>
        public void RecordFirstKill(string playerUid, string trialKey)
        {
            var data = GetOrCreate(playerUid);
            if (data.killedTrialKeys.Add(trialKey))
            {
                dirty = true;
            }
        }

        /// <summary>
        /// Get the reputation rank name for a player.
        /// </summary>
        public string GetRankName(string playerUid)
        {
            int rep = GetReputation(playerUid);
            return GetRankNameForReputation(rep);
        }

        /// <summary>
        /// Get rank name for a given reputation value.
        /// </summary>
        public static string GetRankNameForReputation(int reputation)
        {
            if (reputation >= 1000) return "Покоритель Бездны";
            if (reputation >= 600) return "Мастер Пустоты";
            if (reputation >= 300) return "Закалённый";
            if (reputation >= 100) return "Испытанный";
            return "Новичок";
        }

        /// <summary>
        /// Get the max tier accessible for a player based on reputation.
        /// </summary>
        public int GetMaxAccessibleTier(string playerUid)
        {
            int rep = GetReputation(playerUid);
            if (rep >= 300) return 3;
            if (rep >= 100) return 2;
            return 1;
        }

        /// <summary>
        /// Process rewards for a trial boss kill.
        /// </summary>
        public void ProcessBossKill(string playerUid, HollowTrialConfig config, List<string> completedChallenges)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || config == null) return;

            // Base reputation + shards by tier
            int baseAmount = config.tier switch
            {
                1 => 15,
                2 => 30,
                3 => 50,
                _ => 15
            };
            AddReputationAndShards(playerUid, baseAmount);

            // First kill bonus
            if (!HasKilledBefore(playerUid, config.trialKey))
            {
                RecordFirstKill(playerUid, config.trialKey);
                AddReputationAndShards(playerUid, 30);
            }

            // Challenge bonuses
            int challengeBonus = TrialChallengeEvaluator.CalculateChallengeReputation(config.tier, completedChallenges?.Count ?? 0);
            if (challengeBonus > 0)
            {
                AddReputationAndShards(playerUid, challengeBonus);
            }
        }

        // ---- Persistence ----

        public void Save()
        {
            if (!dirty || sapi == null) return;

            try
            {
                sapi.WorldManager.SaveGame.StoreData(SaveKey, playerData);
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[TrialReputationManager] Failed to save: {0}", ex.Message);
            }

            dirty = false;
        }

        private void Load()
        {
            try
            {
                playerData = sapi.WorldManager.SaveGame.GetData<Dictionary<string, TrialPlayerData>>(SaveKey, null);
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[TrialReputationManager] Failed to load: {0}", ex.Message);
            }

            playerData ??= new Dictionary<string, TrialPlayerData>(StringComparer.OrdinalIgnoreCase);
        }

        private TrialPlayerData GetOrCreate(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid)) return new TrialPlayerData();

            if (!playerData.TryGetValue(playerUid, out var data))
            {
                data = new TrialPlayerData();
                playerData[playerUid] = data;
                dirty = true;
            }

            return data;
        }
    }

    /// <summary>
    /// Per-player trial progression data.
    /// </summary>
    [ProtoBuf.ProtoContract(ImplicitFields = ProtoBuf.ImplicitFields.AllPublic)]
    public class TrialPlayerData
    {
        /// <summary>
        /// Reputation (never spent, only grows).
        /// </summary>
        public int reputation;

        /// <summary>
        /// Void Shards (currency, spent in shop).
        /// </summary>
        public int voidShards;

        /// <summary>
        /// Set of trialKeys the player has killed at least once (for first-kill bonus).
        /// </summary>
        public HashSet<string> killedTrialKeys = new(StringComparer.OrdinalIgnoreCase);
    }
}
