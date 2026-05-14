using System;
using System.Collections.Generic;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Validates promo code redemption: checks existence, type limits, conditions, and rate limiting.
    /// </summary>
    public class PromoCodeValidator
    {
        private readonly PromoCodeStorage storage;
        private readonly PromoCodeSettings settings;
        private readonly ICoreServerAPI sapi;

        // Rate limiting: playerUid -> list of attempt timestamps
        private readonly Dictionary<string, List<DateTime>> attemptHistory = new Dictionary<string, List<DateTime>>();
        // Lockout: playerUid -> lockout expiry time
        private readonly Dictionary<string, DateTime> lockouts = new Dictionary<string, DateTime>();

        public PromoCodeValidator(PromoCodeStorage storage, PromoCodeSettings settings, ICoreServerAPI sapi)
        {
            this.storage = storage;
            this.settings = settings;
            this.sapi = sapi;
        }

        /// <summary>
        /// Validate a promo code for a specific player.
        /// Returns null if valid, or an error lang key if invalid.
        /// </summary>
        public string Validate(PromoCode promoCode, IServerPlayer player)
        {
            string playerUid = player.PlayerUID;

            // Check lockout
            if (IsLockedOut(playerUid))
            {
                return "alegacyvsquest:promo-error-lockout";
            }

            // Check enabled
            if (!promoCode.enabled)
            {
                RecordFailedAttempt(playerUid);
                return "alegacyvsquest:promo-error-disabled";
            }

            // Check date conditions
            var conditions = promoCode.conditions;
            if (conditions != null)
            {
                if (!string.IsNullOrEmpty(conditions.validFrom))
                {
                    if (DateTime.TryParse(conditions.validFrom, out var from) && DateTime.UtcNow < from)
                    {
                        RecordFailedAttempt(playerUid);
                        return "alegacyvsquest:promo-error-not-yet";
                    }
                }

                if (!string.IsNullOrEmpty(conditions.validUntil))
                {
                    if (DateTime.TryParse(conditions.validUntil, out var until) && DateTime.UtcNow > until)
                    {
                        RecordFailedAttempt(playerUid);
                        return "alegacyvsquest:promo-error-expired";
                    }
                }

                // Check required quests
                if (conditions.requiredQuests != null && conditions.requiredQuests.Count > 0)
                {
                    var completedArr = player.Entity?.WatchedAttributes?.GetStringArray(
                        QuestGiverConstants.PlayerCompletedQuestsKey, Array.Empty<string>()) ?? Array.Empty<string>();
                    var completedQuests = new HashSet<string>(completedArr, StringComparer.OrdinalIgnoreCase);

                    foreach (var reqQuest in conditions.requiredQuests)
                    {
                        if (!completedQuests.Contains(reqQuest))
                        {
                            RecordFailedAttempt(playerUid);
                            return "alegacyvsquest:promo-error-quest-required";
                        }
                    }
                }
            }

            // Check usage based on type
            switch (promoCode.type?.ToLowerInvariant())
            {
                case "single":
                    if (storage.GetGlobalUseCount(promoCode.code) >= 1)
                    {
                        RecordFailedAttempt(playerUid);
                        return "alegacyvsquest:promo-error-already-used-global";
                    }
                    break;

                case "personal":
                    if (storage.HasPlayerUsed(playerUid, promoCode.code))
                    {
                        RecordFailedAttempt(playerUid);
                        return "alegacyvsquest:promo-error-already-used";
                    }
                    break;

                case "multi":
                    if (promoCode.maxUses > 0 && storage.GetGlobalUseCount(promoCode.code) >= promoCode.maxUses)
                    {
                        RecordFailedAttempt(playerUid);
                        return "alegacyvsquest:promo-error-max-uses";
                    }
                    if (storage.HasPlayerUsed(playerUid, promoCode.code))
                    {
                        RecordFailedAttempt(playerUid);
                        return "alegacyvsquest:promo-error-already-used";
                    }
                    break;

                case "unlimited":
                    // Check if player already used (each player can use once even for unlimited)
                    if (storage.HasPlayerUsed(playerUid, promoCode.code))
                    {
                        RecordFailedAttempt(playerUid);
                        return "alegacyvsquest:promo-error-already-used";
                    }
                    break;

                default:
                    RecordFailedAttempt(playerUid);
                    return "alegacyvsquest:promo-error-invalid-type";
            }

            return null; // Valid
        }

        /// <summary>
        /// Check rate limiting. Returns true if the player is currently locked out.
        /// </summary>
        public bool IsLockedOut(string playerUid)
        {
            if (lockouts.TryGetValue(playerUid, out var expiry))
            {
                if (DateTime.UtcNow < expiry) return true;
                lockouts.Remove(playerUid);
            }
            return false;
        }

        private void RecordFailedAttempt(string playerUid)
        {
            if (!attemptHistory.TryGetValue(playerUid, out var attempts))
            {
                attempts = new List<DateTime>();
                attemptHistory[playerUid] = attempts;
            }

            var now = DateTime.UtcNow;
            attempts.Add(now);

            // Clean old entries (older than 1 minute)
            attempts.RemoveAll(t => (now - t).TotalMinutes > 1);

            // Check rate limit
            if (attempts.Count >= settings.maxAttemptsPerMinute)
            {
                lockouts[playerUid] = now.AddMinutes(settings.lockoutMinutes);
                attempts.Clear();
                sapi.Logger.Notification("[PromoCode] Player {0} locked out for {1} minutes (rate limit exceeded)", playerUid, settings.lockoutMinutes);
            }
        }
    }
}
