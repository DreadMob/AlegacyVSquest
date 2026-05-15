using System;
using System.Collections.Generic;
using ProtoBuf;

namespace VsQuest
{
    /// <summary>
    /// Configuration for a single Hollow Trial boss.
    /// Each boss supports all 3 tiers with different stats/challenges.
    /// Loaded from config/hollowtrials/*.json per mod.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class HollowTrialConfig
    {
        /// <summary>
        /// Unique key identifying this trial boss (e.g. "albase:trial:shadow-stalker").
        /// </summary>
        public string trialKey;

        /// <summary>
        /// Entity code for this boss (e.g. "albase:shadow-stalker").
        /// If null, derived from trialKey.
        /// </summary>
        public string entityCode;

        /// <summary>
        /// Per-tier configuration. Key = tier (1, 2, 3).
        /// </summary>
        public Dictionary<int, HollowTrialTierData> tiers = new();

        /// <summary>
        /// Hours after death before the boss can respawn. 0 = use global default.
        /// </summary>
        public double respawnInGameHours;

        /// <summary>
        /// Distance in blocks at which a player triggers boss spawn. 0 = use global default.
        /// </summary>
        public float activationRange;

        /// <summary>
        /// Hours without damage before soft reset (despawn + full HP). 0 = use global default.
        /// </summary>
        public double softResetIdleHours;

        /// <summary>
        /// Get entity code, deriving from trialKey if not explicitly set.
        /// </summary>
        public string GetEntityCode()
        {
            if (!string.IsNullOrWhiteSpace(entityCode)) return entityCode;
            return DeriveEntityCodeFromTrialKey(trialKey);
        }

        private static string DeriveEntityCodeFromTrialKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;

            // "albase:trial:shadow-stalker" -> "albase:shadow-stalker"
            var parts = key.Split(':');
            if (parts.Length == 2) return key;
            if (parts.Length < 2) return null;

            var domain = parts[0];
            var bossName = parts[parts.Length - 1];
            return domain + ":" + bossName;
        }

        /// <summary>
        /// Get tier data for a specific tier. Returns null if tier not configured.
        /// </summary>
        public HollowTrialTierData GetTierData(int tier)
        {
            if (tiers == null) return null;
            return tiers.TryGetValue(tier, out var data) ? data : null;
        }

        /// <summary>
        /// Get challenges for a specific tier.
        /// </summary>
        public List<HollowTrialChallenge> GetChallenges(int tier)
        {
            var data = GetTierData(tier);
            return data?.challenges ?? new List<HollowTrialChallenge>();
        }

        /// <summary>
        /// Get quest ID for a specific tier.
        /// </summary>
        public string GetQuestId(int tier)
        {
            var data = GetTierData(tier);
            return data?.questId;
        }

        /// <summary>
        /// Get max HP for a specific tier.
        /// </summary>
        public float GetMaxHealth(int tier)
        {
            var data = GetTierData(tier);
            return data?.maxHealth ?? 300;
        }

        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(trialKey)) return false;
            if (tiers == null || tiers.Count == 0) return false;
            // Must have at least tier 1
            if (!tiers.ContainsKey(1)) return false;
            // Each tier must have a questId
            foreach (var kvp in tiers)
            {
                if (kvp.Key < 1 || kvp.Key > 3) return false;
                if (string.IsNullOrWhiteSpace(kvp.Value?.questId)) return false;
            }
            return true;
        }

        public double GetRespawnHours(HollowTrialCoreConfig coreConfig)
        {
            return respawnInGameHours > 0 ? respawnInGameHours : (coreConfig?.DefaultRespawnHours ?? 168);
        }

        public float GetActivationRange(HollowTrialCoreConfig coreConfig)
        {
            return activationRange > 0 ? activationRange : (coreConfig?.DefaultActivationRange ?? 120f);
        }

        public double GetSoftResetIdleHours(HollowTrialCoreConfig coreConfig)
        {
            return softResetIdleHours > 0 ? softResetIdleHours : (coreConfig?.SoftResetIdleHours ?? 2.0);
        }
    }

    /// <summary>
    /// Per-tier data for a trial boss.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class HollowTrialTierData
    {
        /// <summary>
        /// Quest ID for this tier (e.g. "albase:trial-tier1").
        /// </summary>
        public string questId;

        /// <summary>
        /// Max HP for this tier. Applied when spawning.
        /// </summary>
        public float maxHealth = 300;

        /// <summary>
        /// Damage multiplier for this tier (1.0 = base).
        /// </summary>
        public float damageMult = 1.0f;

        /// <summary>
        /// Speed multiplier for this tier (1.0 = base).
        /// </summary>
        public float speedMult = 1.0f;

        /// <summary>
        /// Enrage timer in seconds for this tier.
        /// </summary>
        public int enrageTimerSeconds = 240;

        /// <summary>
        /// Challenge definitions for this tier.
        /// </summary>
        public List<HollowTrialChallenge> challenges = new();
    }

    /// <summary>
    /// A single challenge condition for a trial boss.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class HollowTrialChallenge
    {
        /// <summary>
        /// Challenge type: "speedkill", "deathless", "nofood", "lowgear", "perfectdodge".
        /// </summary>
        public string type;

        /// <summary>
        /// Threshold in minutes for speedkill challenge.
        /// </summary>
        public double thresholdMinutes;

        /// <summary>
        /// Max armor tier for lowgear challenge.
        /// </summary>
        public int maxArmorTier;

        /// <summary>
        /// Ability code for perfectdodge challenge.
        /// </summary>
        public string abilityCode;
    }
}
