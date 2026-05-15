using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Tracks promo code usage per player and globally.
    /// Persists via mod config (promocodes-usage.json).
    /// </summary>
    public class PromoCodeStorage
    {
        private const string StorageFile = "alegacyvsquest/promocodes-usage.json";

        private readonly ICoreServerAPI sapi;
        private PromoCodeUsageData data;

        public PromoCodeStorage(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            Load();
        }

        /// <summary>
        /// Check if a player has already used a specific code.
        /// </summary>
        public bool HasPlayerUsed(string playerUid, string code)
        {
            string key = NormalizeCode(code);
            if (!data.playerUsage.TryGetValue(playerUid, out var usedCodes)) return false;
            return usedCodes.Contains(key);
        }

        /// <summary>
        /// Get total number of times a code has been used globally.
        /// </summary>
        public int GetGlobalUseCount(string code)
        {
            string key = NormalizeCode(code);
            data.globalUsage.TryGetValue(key, out int count);
            return count;
        }

        /// <summary>
        /// Record that a player has used a code.
        /// </summary>
        public void RecordUsage(string playerUid, string code)
        {
            string key = NormalizeCode(code);

            // Player usage
            if (!data.playerUsage.TryGetValue(playerUid, out var usedCodes))
            {
                usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                data.playerUsage[playerUid] = usedCodes;
            }
            usedCodes.Add(key);

            // Global usage
            if (!data.globalUsage.ContainsKey(key))
            {
                data.globalUsage[key] = 0;
            }
            data.globalUsage[key]++;

            // Log entry
            data.log.Add(new PromoCodeLogEntry
            {
                playerUid = playerUid,
                code = code,
                timestamp = DateTime.UtcNow.ToString("o")
            });

            Save();
        }

        /// <summary>
        /// Get usage log for a specific code.
        /// </summary>
        public List<PromoCodeLogEntry> GetLogForCode(string code)
        {
            string key = NormalizeCode(code);
            var result = new List<PromoCodeLogEntry>();
            foreach (var entry in data.log)
            {
                if (string.Equals(entry.code, code, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(entry);
                }
            }
            return result;
        }

        private string NormalizeCode(string code)
        {
            return code?.ToUpperInvariant() ?? "";
        }

        /// <summary>
        /// Reset a player's usage of a specific code so they can redeem it again.
        /// </summary>
        public bool ResetPlayerUsage(string playerUid, string code)
        {
            string key = NormalizeCode(code);
            bool removed = false;

            if (data.playerUsage.TryGetValue(playerUid, out var usedCodes))
            {
                removed = usedCodes.Remove(key);
            }

            // Decrement global count
            if (removed && data.globalUsage.ContainsKey(key) && data.globalUsage[key] > 0)
            {
                data.globalUsage[key]--;
            }

            if (removed)
            {
                Save();
            }

            return removed;
        }

        private void Load()
        {
            try
            {
                data = sapi.LoadModConfig<PromoCodeUsageData>(StorageFile);
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[PromoCode] Failed to load usage data: {0}", ex.Message);
                data = null;
            }

            if (data == null)
            {
                data = new PromoCodeUsageData();
                Save();
            }
        }

        private void Save()
        {
            try
            {
                sapi.StoreModConfig(data, StorageFile);
            }
            catch (Exception ex)
            {
                sapi.Logger.Error("[PromoCode] Failed to save usage data: {0}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Persisted usage data.
    /// </summary>
    public class PromoCodeUsageData
    {
        /// <summary>playerUid -> set of used code keys (uppercase).</summary>
        public Dictionary<string, HashSet<string>> playerUsage { get; set; } = new Dictionary<string, HashSet<string>>();

        /// <summary>code key (uppercase) -> total use count.</summary>
        public Dictionary<string, int> globalUsage { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Full log of all redemptions.</summary>
        public List<PromoCodeLogEntry> log { get; set; } = new List<PromoCodeLogEntry>();
    }

    public class PromoCodeLogEntry
    {
        public string playerUid { get; set; }
        public string code { get; set; }
        public string timestamp { get; set; }
    }
}
