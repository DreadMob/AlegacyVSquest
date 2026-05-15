using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Core promo code system. Loads configs, manages code registry, handles redemption.
    /// Codes can come from two sources:
    /// 1. Asset-based: config/promocodes.json (from mod assets, read-only)
    /// 2. Runtime: promocodes-runtime.json (created via admin commands, persisted in mod config)
    /// </summary>
    public class PromoCodeSystem
    {
        private const string RuntimeConfigFile = "alegacyvsquest/promocodes-runtime.json";

        private readonly ICoreServerAPI sapi;
        private readonly ItemSystem itemSystem;
        private readonly QuestSystem questSystem;

        private PromoCodeSettings settings = new PromoCodeSettings();
        private readonly Dictionary<string, PromoCode> codeRegistry = new Dictionary<string, PromoCode>(StringComparer.OrdinalIgnoreCase);

        private PromoCodeStorage storage;
        private PromoCodeValidator validator;
        private PromoCodeRewardGiver rewardGiver;

        // Runtime codes (created via commands, persisted separately)
        private PromoCodeConfig runtimeConfig;

        public PromoCodeSystem(ICoreServerAPI sapi, ItemSystem itemSystem, QuestSystem questSystem)
        {
            this.sapi = sapi;
            this.itemSystem = itemSystem;
            this.questSystem = questSystem;
        }

        /// <summary>
        /// Initialize the system: load configs, create services.
        /// </summary>
        public void Initialize()
        {
            LoadAssetConfigs();
            LoadRuntimeConfig();

            storage = new PromoCodeStorage(sapi);
            validator = new PromoCodeValidator(storage, settings, sapi);
            rewardGiver = new PromoCodeRewardGiver(sapi, itemSystem, questSystem);

            sapi.Logger.Notification("[PromoCode] System initialized with {0} codes ({1} asset, {2} runtime)",
                codeRegistry.Count,
                codeRegistry.Count - (runtimeConfig?.codes?.Count ?? 0),
                runtimeConfig?.codes?.Count ?? 0);
        }

        /// <summary>
        /// Attempt to redeem a promo code for a player.
        /// Returns (success, message lang key or text).
        /// </summary>
        public (bool success, string message) Redeem(string code, IServerPlayer player)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return (false, "alegacyvsquest:promo-error-empty");
            }

            string lookupKey = settings.caseInsensitive ? code.ToUpperInvariant() : code;

            if (!codeRegistry.TryGetValue(lookupKey, out var promoCode))
            {
                // Don't reveal whether code exists or not (security)
                return (false, "alegacyvsquest:promo-error-invalid");
            }

            // Validate
            string error = validator.Validate(promoCode, player);
            if (error != null)
            {
                return (false, error);
            }

            // Give rewards
            var rewardResults = rewardGiver.GiveRewards(promoCode, player);

            // Record usage
            storage.RecordUsage(player.PlayerUID, promoCode.code);

            // Sync to database
            SyncRedemptionToDb(player, promoCode, rewardResults);

            sapi.Logger.Notification("[PromoCode] Player {0} redeemed code '{1}'. Rewards: {2}",
                player.PlayerName, promoCode.code, string.Join(", ", rewardResults));

            // Return success message
            string successMsg = !string.IsNullOrEmpty(promoCode.message)
                ? promoCode.message
                : "alegacyvsquest:promo-success";

            return (true, successMsg);
        }

        /// <summary>
        /// Create a new runtime promo code (via admin command).
        /// </summary>
        public (bool success, string message) CreateCode(PromoCode newCode)
        {
            if (string.IsNullOrWhiteSpace(newCode.code))
            {
                return (false, "Code cannot be empty.");
            }

            string key = settings.caseInsensitive ? newCode.code.ToUpperInvariant() : newCode.code;

            if (codeRegistry.ContainsKey(key))
            {
                return (false, $"Code '{newCode.code}' already exists.");
            }

            codeRegistry[key] = newCode;
            runtimeConfig.codes.Add(newCode);
            SaveRuntimeConfig();

            return (true, $"Code '{newCode.code}' created successfully.");
        }

        /// <summary>
        /// Delete a runtime promo code.
        /// </summary>
        public (bool success, string message) DeleteCode(string code)
        {
            string key = settings.caseInsensitive ? code.ToUpperInvariant() : code;

            if (!codeRegistry.ContainsKey(key))
            {
                return (false, $"Code '{code}' not found.");
            }

            codeRegistry.Remove(key);
            runtimeConfig.codes.RemoveAll(c => string.Equals(c.code, code, StringComparison.OrdinalIgnoreCase));
            SaveRuntimeConfig();

            return (true, $"Code '{code}' deleted.");
        }

        /// <summary>
        /// Reset a player's usage of a code so they can redeem it again.
        /// </summary>
        public (bool success, string message) ResetPlayerUsage(string playerUid, string playerName, string code)
        {
            if (string.IsNullOrWhiteSpace(playerUid))
            {
                return (false, "Player UID is required.");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return (false, "Code is required.");
            }

            bool removed = storage.ResetPlayerUsage(playerUid, code);
            if (removed)
            {
                sapi.Logger.Notification("[PromoCode] Admin reset code '{0}' for player {1} ({2})", code, playerName ?? playerUid, playerUid);
                return (true, $"Reset code '{code}' for player '{playerName ?? playerUid}'. They can redeem it again.");
            }

            return (false, $"Player '{playerName ?? playerUid}' has not used code '{code}'.");
        }

        /// <summary>
        /// Add a reward to an existing code at runtime.
        /// </summary>
        public (bool success, string message) AddReward(string code, PromoCodeReward reward)
        {
            string key = settings.caseInsensitive ? code.ToUpperInvariant() : code;

            if (!codeRegistry.TryGetValue(key, out var promoCode))
            {
                return (false, $"Code '{code}' not found.");
            }

            promoCode.rewards.Add(reward);

            // If it's a runtime code, save
            var runtimeCode = runtimeConfig.codes.FirstOrDefault(c => string.Equals(c.code, code, StringComparison.OrdinalIgnoreCase));
            if (runtimeCode != null)
            {
                runtimeCode.rewards.Add(reward);
                SaveRuntimeConfig();
            }

            return (true, $"Reward added to code '{code}'.");
        }

        /// <summary>
        /// Get all registered codes (for admin listing).
        /// </summary>
        public IEnumerable<PromoCode> GetAllCodes()
        {
            return codeRegistry.Values;
        }

        /// <summary>
        /// Get info about a specific code.
        /// </summary>
        public PromoCode GetCode(string code)
        {
            string key = settings.caseInsensitive ? code.ToUpperInvariant() : code;
            codeRegistry.TryGetValue(key, out var result);
            return result;
        }

        /// <summary>
        /// Get usage count for a code.
        /// </summary>
        public int GetUsageCount(string code)
        {
            return storage.GetGlobalUseCount(code);
        }

        /// <summary>
        /// Reload all configs (asset + runtime).
        /// </summary>
        public void Reload()
        {
            codeRegistry.Clear();
            LoadAssetConfigs();
            LoadRuntimeConfig();
            sapi.Logger.Notification("[PromoCode] Reloaded. Total codes: {0}", codeRegistry.Count);
        }

        private void SyncRedemptionToDb(IServerPlayer player, PromoCode promoCode, List<string> rewardResults)
        {
            try
            {
                var dbClient = questSystem.GetDbClient();
                if (dbClient == null || !dbClient.IsEnabled) return;

                Task.Run(async () =>
                {
                    try
                    {
                        var body = new
                        {
                            player_uid = player.PlayerUID,
                            player_name = player.PlayerName,
                            code = promoCode.code,
                            rewards_json = string.Join(", ", rewardResults),
                        };

                        await dbClient.PostAsync("/vsquest/promo-redemptions", body);
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Warning("[PromoCode] Failed to sync redemption to DB: {0}", ex.Message);
                    }
                });
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[PromoCode] Exception in SyncRedemptionToDb: {0}", ex.Message);
            }
        }

        private void LoadAssetConfigs()
        {
            try
            {
                foreach (var mod in sapi.ModLoader.Mods)
                {
                    var assets = sapi.Assets.GetMany<PromoCodeConfig>(sapi.Logger, "config/promocodes", mod.Info.ModID);
                    foreach (var asset in assets)
                    {
                        if (asset.Value == null) continue;

                        // Use settings from the first config that has them
                        if (asset.Value.settings != null)
                        {
                            settings = asset.Value.settings;
                        }

                        foreach (var code in asset.Value.codes)
                        {
                            string key = settings.caseInsensitive ? code.code?.ToUpperInvariant() : code.code;
                            if (!string.IsNullOrEmpty(key))
                            {
                                codeRegistry[key] = code;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Error("[PromoCode] Failed to load asset configs: {0}", ex.Message);
            }
        }

        private void LoadRuntimeConfig()
        {
            try
            {
                runtimeConfig = sapi.LoadModConfig<PromoCodeConfig>(RuntimeConfigFile);
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[PromoCode] Failed to load runtime config: {0}", ex.Message);
                runtimeConfig = null;
            }

            if (runtimeConfig == null)
            {
                runtimeConfig = new PromoCodeConfig();
                SaveRuntimeConfig();
            }

            foreach (var code in runtimeConfig.codes)
            {
                string key = settings.caseInsensitive ? code.code?.ToUpperInvariant() : code.code;
                if (!string.IsNullOrEmpty(key))
                {
                    codeRegistry[key] = code;
                }
            }
        }

        private void SaveRuntimeConfig()
        {
            try
            {
                sapi.StoreModConfig(runtimeConfig, RuntimeConfigFile);
            }
            catch (Exception ex)
            {
                sapi.Logger.Error("[PromoCode] Failed to save runtime config: {0}", ex.Message);
            }
        }
    }
}
