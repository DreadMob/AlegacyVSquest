using System;
using System.Collections.Generic;
using ProtoBuf;

namespace VsQuest
{
    /// <summary>
    /// Configuration for a single Hollow Trial boss.
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
        /// Quest ID associated with this trial (e.g. "albase:trial-shadow-stalker").
        /// </summary>
        public string questId;

        /// <summary>
        /// Difficulty tier: 1 = Hollow, 2 = Deep, 3 = Abyssal.
        /// </summary>
        public int tier = 1;

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
        /// Challenge definitions for this boss.
        /// </summary>
        public List<HollowTrialChallenge> challenges = new();

        public string GetEntityCode()
        {
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

        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(trialKey)) return false;
            if (string.IsNullOrWhiteSpace(questId)) return false;
            if (tier < 1 || tier > 3) return false;
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
    /// A single challenge condition for a trial boss.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class HollowTrialChallenge
    {
        /// <summary>
        /// Challenge type: "speedkill", "deathless", "nopotions", "lowgear", "perfectdodge".
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
