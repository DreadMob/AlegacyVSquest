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
        /// Add reputation only.
        /// </summary>
        public void AddReputation(string playerUid, int amount)
        {
            if (amount <= 0) return;
            var data = GetOrCreate(playerUid);
            data.reputation += amount;
            dirty = true;
        }

        /// <summary>
        /// Add void shards only.
        /// </summary>
        public void AddVoidShards(string playerUid, int amount)
        {
            if (amount <= 0) return;
            var data = GetOrCreate(playerUid);
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
        /// Refund void shards (used when a purchase fails after spending).
        /// </summary>
        public void RefundVoidShards(string playerUid, int amount)
        {
            if (amount <= 0) return;
            var data = GetOrCreate(playerUid);
            data.voidShards += amount;
            dirty = true;
        }

        /// <summary>
        /// Get number of times a player purchased a specific shop item.
        /// </summary>
        public int GetPurchaseCount(string playerUid, string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return 0;
            var data = GetOrCreate(playerUid);
            return data.purchaseCounts.TryGetValue(itemCode, out var count) ? count : 0;
        }

        /// <summary>
        /// Record a shop purchase for the player.
        /// </summary>
        public void RecordPurchase(string playerUid, string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return;
            var data = GetOrCreate(playerUid);
            data.purchaseCounts.TryGetValue(itemCode, out var prev);
            data.purchaseCounts[itemCode] = prev + 1;
            dirty = true;
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
        public void ProcessBossKill(string playerUid, HollowTrialConfig config, int tier, List<string> completedChallenges, TrialModifierType activeModifier = TrialModifierType.None)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || config == null) return;

            float shardMult = TrialWeeklyModifierUtils.GetShardMultiplier(activeModifier);
            float repMult = TrialWeeklyModifierUtils.GetReputationMultiplier(activeModifier);

            // Base reputation + shards by tier
            int baseAmount = tier switch
            {
                1 => 15,
                2 => 30,
                3 => 50,
                _ => 15
            };

            int repReward = (int)Math.Round(baseAmount * repMult);
            int shardReward = (int)Math.Round(baseAmount * shardMult);
            AddReputation(playerUid, repReward);
            AddVoidShards(playerUid, shardReward);

            // First kill bonus (per boss, not per tier)
            if (!HasKilledBefore(playerUid, config.trialKey))
            {
                RecordFirstKill(playerUid, config.trialKey);
                int firstKillRep = (int)Math.Round(30 * repMult);
                int firstKillShards = (int)Math.Round(30 * shardMult);
                AddReputation(playerUid, firstKillRep);
                AddVoidShards(playerUid, firstKillShards);
            }

            // Challenge bonuses
            int challengeBonus = TrialChallengeEvaluator.CalculateChallengeReputation(tier, completedChallenges?.Count ?? 0);
            if (challengeBonus > 0)
            {
                int challengeRep = (int)Math.Round(challengeBonus * repMult);
                int challengeShards = (int)Math.Round(challengeBonus * shardMult);
                AddReputation(playerUid, challengeRep);
                AddVoidShards(playerUid, challengeShards);
            }
        }

        /// <summary>
        /// Record a boss kill result, updating best stats if improved.
        /// </summary>
        public void RecordBestResult(string playerUid, string trialKey, int tier, double durationMinutes, int challengeCount, bool wasDeathless)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || string.IsNullOrWhiteSpace(trialKey)) return;

            var data = GetOrCreate(playerUid);
            string key = $"{trialKey}:{tier}";

            if (!data.bestResults.TryGetValue(key, out var best))
            {
                best = new TrialBestResult();
                data.bestResults[key] = best;
            }

            best.totalKills++;
            if (wasDeathless) best.deathlessKills++;
            if (durationMinutes > 0 && durationMinutes < best.bestTimeMinutes) best.bestTimeMinutes = durationMinutes;
            if (challengeCount > best.bestChallengesCount) best.bestChallengesCount = challengeCount;

            dirty = true;
        }

        /// <summary>
        /// Get best result for a player on a specific boss/tier. Returns null if no kills.
        /// </summary>
        public TrialBestResult GetBestResult(string playerUid, string trialKey, int tier)
        {
            var data = GetOrCreate(playerUid);
            string key = $"{trialKey}:{tier}";
            return data.bestResults.TryGetValue(key, out var best) ? best : null;
        }

        /// <summary>
        /// Get total kill count for a player on a specific boss (across all tiers).
        /// </summary>
        public int GetKillCount(string playerUid, string trialKey)
        {
            var data = GetOrCreate(playerUid);
            return data.killCounts.TryGetValue(trialKey, out var count) ? count : 0;
        }

        /// <summary>
        /// Increment kill count for a player on a specific boss.
        /// </summary>
        public void IncrementKillCount(string playerUid, string trialKey)
        {
            if (string.IsNullOrWhiteSpace(playerUid) || string.IsNullOrWhiteSpace(trialKey)) return;
            var data = GetOrCreate(playerUid);
            data.killCounts.TryGetValue(trialKey, out var prev);
            data.killCounts[trialKey] = prev + 1;
            dirty = true;
        }

        /// <summary>
        /// Get the progressive difficulty multiplier based on kill count.
        /// Every 5 kills adds +5% HP/damage (capped at +50% at 50 kills).
        /// </summary>
        public static float GetKillCountScaling(int killCount)
        {
            if (killCount <= 0) return 1.0f;
            int scalingSteps = Math.Min(killCount / 5, 10); // cap at 10 steps (50 kills)
            return 1.0f + scalingSteps * 0.05f; // +5% per step
        }

        /// <summary>
        /// Get the bonus shard reward for progressive difficulty.
        /// +2 shards per 5 kills (capped at +20 at 50 kills).
        /// </summary>
        public static int GetKillCountBonusShards(int killCount)
        {
            if (killCount <= 0) return 0;
            int steps = Math.Min(killCount / 5, 10);
            return steps * 2;
        }

        /// <summary>
        /// Completely reset all trial data for a player (used by admin qfall command).
        /// </summary>
        public void ResetPlayerData(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid)) return;

            if (playerData.Remove(playerUid))
            {
                dirty = true;
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

        /// <summary>
        /// Per-shop-item purchase counts (for limited-quantity shop items).
        /// </summary>
        public Dictionary<string, int> purchaseCounts = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Per-boss best results. Key = trialKey + ":" + tier (e.g. "albase:trial:shadow-stalker:2").
        /// </summary>
        public Dictionary<string, TrialBestResult> bestResults = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Per-boss kill counts (for progressive difficulty). Key = trialKey.
        /// </summary>
        public Dictionary<string, int> killCounts = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Best result data for a specific boss at a specific tier.
    /// </summary>
    [ProtoBuf.ProtoContract(ImplicitFields = ProtoBuf.ImplicitFields.AllPublic)]
    public class TrialBestResult
    {
        /// <summary>
        /// Best (fastest) kill time in minutes.
        /// </summary>
        public double bestTimeMinutes = double.MaxValue;

        /// <summary>
        /// Total number of kills.
        /// </summary>
        public int totalKills;

        /// <summary>
        /// Number of deathless kills.
        /// </summary>
        public int deathlessKills;

        /// <summary>
        /// Maximum number of challenges completed in a single kill.
        /// </summary>
        public int bestChallengesCount;
    }
}
