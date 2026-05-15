using System;

namespace VsQuest
{
    /// <summary>
    /// Weekly modifier types that affect all trial bosses during a rotation.
    /// One modifier is randomly selected at each rotation.
    /// Mix of positive (player benefit), negative (boss stronger), and combo modifiers.
    /// </summary>
    public enum TrialModifierType
    {
        /// <summary>No modifier active.</summary>
        None = 0,

        // ── Negative (boss stronger) ──────────────────────
        /// <summary>Bosses have +25% HP.</summary>
        BossHpUp = 1,
        /// <summary>Bosses deal +20% damage.</summary>
        BossDamageUp = 2,
        /// <summary>Bosses move 15% faster.</summary>
        BossSpeedUp = 3,
        /// <summary>Enrage timer is 25% shorter.</summary>
        EnrageSpeedup = 5,
        /// <summary>Boss abilities have 20% shorter cooldowns.</summary>
        AbilityCooldownReduced = 8,
        /// <summary>No vulnerability windows at all.</summary>
        NoVulnerability = 9,
        /// <summary>Boss regenerates 1% HP per second out of combat (3s threshold).</summary>
        BossRegen = 10,
        /// <summary>Player healing reduced by 50% during fight.</summary>
        HealingReduced = 11,

        // ── Positive (player benefit) ─────────────────────
        /// <summary>Double Void Shard rewards.</summary>
        DoubleShards = 4,
        /// <summary>Vulnerability windows last 50% longer.</summary>
        VulnerabilityExtended = 6,
        /// <summary>+50% reputation from kills.</summary>
        ReputationBoost = 7,

        // ── Combo (negative + compensation) ───────────────
        /// <summary>Bosses +40% HP but ×2.5 shards.</summary>
        Fortified = 12,
        /// <summary>Bosses +30% damage but vulnerability windows deal ×3 damage.</summary>
        GlassCannon = 13,
        /// <summary>Enrage 40% faster but +75% reputation.</summary>
        Desperate = 14
    }

    /// <summary>
    /// Utility for rolling and applying weekly modifiers.
    /// </summary>
    public static class TrialWeeklyModifierUtils
    {
        private static readonly TrialModifierType[] AllModifiers = new[]
        {
            // Negative
            TrialModifierType.BossHpUp,
            TrialModifierType.BossDamageUp,
            TrialModifierType.BossSpeedUp,
            TrialModifierType.EnrageSpeedup,
            TrialModifierType.AbilityCooldownReduced,
            TrialModifierType.NoVulnerability,
            TrialModifierType.BossRegen,
            TrialModifierType.HealingReduced,
            // Positive
            TrialModifierType.DoubleShards,
            TrialModifierType.VulnerabilityExtended,
            TrialModifierType.ReputationBoost,
            // Combo
            TrialModifierType.Fortified,
            TrialModifierType.GlassCannon,
            TrialModifierType.Desperate
        };

        /// <summary>
        /// Roll a random modifier.
        /// </summary>
        public static TrialModifierType Roll(Random random)
        {
            return AllModifiers[random.Next(AllModifiers.Length)];
        }

        /// <summary>
        /// Get the HP multiplier for the current modifier.
        /// </summary>
        public static float GetHpMultiplier(TrialModifierType modifier)
        {
            return modifier switch
            {
                TrialModifierType.BossHpUp => 1.25f,
                TrialModifierType.Fortified => 1.4f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Get the damage multiplier for the current modifier.
        /// </summary>
        public static float GetDamageMultiplier(TrialModifierType modifier)
        {
            return modifier switch
            {
                TrialModifierType.BossDamageUp => 1.2f,
                TrialModifierType.GlassCannon => 1.3f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Get the speed multiplier for the current modifier.
        /// </summary>
        public static float GetSpeedMultiplier(TrialModifierType modifier)
        {
            return modifier == TrialModifierType.BossSpeedUp ? 1.15f : 1.0f;
        }

        /// <summary>
        /// Get the shard reward multiplier for the current modifier.
        /// </summary>
        public static float GetShardMultiplier(TrialModifierType modifier)
        {
            return modifier switch
            {
                TrialModifierType.DoubleShards => 2.0f,
                TrialModifierType.Fortified => 2.5f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Get the enrage timer multiplier (lower = faster enrage).
        /// </summary>
        public static float GetEnrageTimerMultiplier(TrialModifierType modifier)
        {
            return modifier switch
            {
                TrialModifierType.EnrageSpeedup => 0.75f,
                TrialModifierType.Desperate => 0.6f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Get the vulnerability window duration multiplier.
        /// 0 = no vulnerability windows at all.
        /// </summary>
        public static float GetVulnerabilityDurationMultiplier(TrialModifierType modifier)
        {
            return modifier switch
            {
                TrialModifierType.VulnerabilityExtended => 1.5f,
                TrialModifierType.NoVulnerability => 0f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Get the vulnerability window damage multiplier (how much extra damage during vuln).
        /// </summary>
        public static float GetVulnerabilityDamageMultiplier(TrialModifierType modifier)
        {
            return modifier == TrialModifierType.GlassCannon ? 3.0f : 1.0f;
        }

        /// <summary>
        /// Get the reputation reward multiplier.
        /// </summary>
        public static float GetReputationMultiplier(TrialModifierType modifier)
        {
            return modifier switch
            {
                TrialModifierType.ReputationBoost => 1.5f,
                TrialModifierType.Desperate => 1.75f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Get the ability cooldown multiplier (lower = shorter cooldowns).
        /// </summary>
        public static float GetAbilityCooldownMultiplier(TrialModifierType modifier)
        {
            return modifier == TrialModifierType.AbilityCooldownReduced ? 0.8f : 1.0f;
        }

        /// <summary>
        /// Whether boss passive regen is active (BossRegen modifier).
        /// </summary>
        public static bool IsBossRegenActive(TrialModifierType modifier)
        {
            return modifier == TrialModifierType.BossRegen;
        }

        /// <summary>
        /// Get player healing effectiveness multiplier.
        /// </summary>
        public static float GetPlayerHealingMultiplier(TrialModifierType modifier)
        {
            return modifier == TrialModifierType.HealingReduced ? 0.5f : 1.0f;
        }

        /// <summary>
        /// Get localization key for the modifier name.
        /// </summary>
        public static string GetNameKey(TrialModifierType modifier)
        {
            return modifier switch
            {
                TrialModifierType.BossHpUp => "albase:trial-modifier-bosshpup",
                TrialModifierType.BossDamageUp => "albase:trial-modifier-bossdamageup",
                TrialModifierType.BossSpeedUp => "albase:trial-modifier-bossspeedup",
                TrialModifierType.DoubleShards => "albase:trial-modifier-doubleshards",
                TrialModifierType.EnrageSpeedup => "albase:trial-modifier-enragespeedup",
                TrialModifierType.VulnerabilityExtended => "albase:trial-modifier-vulnextended",
                TrialModifierType.ReputationBoost => "albase:trial-modifier-repboost",
                TrialModifierType.AbilityCooldownReduced => "albase:trial-modifier-abilitycdr",
                TrialModifierType.NoVulnerability => "albase:trial-modifier-novuln",
                TrialModifierType.BossRegen => "albase:trial-modifier-bossregen",
                TrialModifierType.HealingReduced => "albase:trial-modifier-healingreduced",
                TrialModifierType.Fortified => "albase:trial-modifier-fortified",
                TrialModifierType.GlassCannon => "albase:trial-modifier-glasscannon",
                TrialModifierType.Desperate => "albase:trial-modifier-desperate",
                _ => "albase:trial-modifier-none"
            };
        }
    }
}
