using System;
using System.Collections.Generic;

namespace VsQuest
{
    /// <summary>
    /// Determines the quality tier of a trial reward item.
    /// 4 qualities: Dim (1.0x), Shimmering (1.2x), Radiant (1.45x), Abyssal (1.75x).
    /// Chances are modified by boss tier and completed challenges.
    /// </summary>
    public static class TrialQualityRoller
    {
        public const int QualityDim = 1;
        public const int QualityShimmering = 2;
        public const int QualityRadiant = 3;
        public const int QualityAbyssal = 4;

        public static readonly Dictionary<int, string> QualityNames = new()
        {
            { QualityDim, "Dim" },
            { QualityShimmering, "Shimmering" },
            { QualityRadiant, "Radiant" },
            { QualityAbyssal, "Abyssal" }
        };

        public static readonly Dictionary<int, float> QualityMultipliers = new()
        {
            { QualityDim, 1.0f },
            { QualityShimmering, 1.2f },
            { QualityRadiant, 1.45f },
            { QualityAbyssal, 1.75f }
        };

        /// <summary>
        /// Roll a quality tier based on boss tier and completed challenges.
        /// </summary>
        /// <param name="bossTier">Boss difficulty tier (1-3).</param>
        /// <param name="completedChallenges">List of completed challenge type strings.</param>
        /// <param name="random">Random instance for rolling.</param>
        /// <returns>Quality tier (1-4).</returns>
        public static int Roll(int bossTier, List<string> completedChallenges, Random random)
        {
            // Base chances (percentages)
            float dimChance = 50f;
            float shimmeringChance = 30f;
            float radiantChance = 15f;
            float abyssalChance = 5f;

            // Tier bonuses (subtracted from Dim)
            switch (bossTier)
            {
                case 2:
                    shimmeringChance += 5f;
                    radiantChance += 3f;
                    dimChance -= 8f;
                    break;
                case 3:
                    shimmeringChance += 10f;
                    radiantChance += 5f;
                    abyssalChance += 2f;
                    dimChance -= 17f;
                    break;
            }

            // Challenge bonuses
            int challengeCount = completedChallenges?.Count ?? 0;
            if (challengeCount == 1)
            {
                shimmeringChance += 5f;
                dimChance -= 5f;
            }
            else if (challengeCount >= 2)
            {
                shimmeringChance += 10f;
                dimChance -= 10f;
            }

            // Deathless + speedkill combo bonus
            if (completedChallenges != null &&
                completedChallenges.Contains("deathless") &&
                completedChallenges.Contains("speedkill"))
            {
                abyssalChance += 3f;
                dimChance -= 3f;
            }

            // Enforce minimum 10% Dim
            if (dimChance < 10f)
            {
                float overflow = 10f - dimChance;
                dimChance = 10f;

                // Proportionally reduce bonuses
                float totalBonus = shimmeringChance + radiantChance + abyssalChance - 50f; // original non-dim total
                if (totalBonus > 0)
                {
                    float shimBonus = shimmeringChance - 30f;
                    float radBonus = radiantChance - 15f;
                    float abysBonus = abyssalChance - 5f;

                    float scale = (totalBonus - overflow) / totalBonus;
                    if (scale < 0) scale = 0;

                    shimmeringChance = 30f + shimBonus * scale;
                    radiantChance = 15f + radBonus * scale;
                    abyssalChance = 5f + abysBonus * scale;
                }
            }

            // Normalize to 100%
            float total = dimChance + shimmeringChance + radiantChance + abyssalChance;
            if (Math.Abs(total - 100f) > 0.01f)
            {
                dimChance = dimChance / total * 100f;
                shimmeringChance = shimmeringChance / total * 100f;
                radiantChance = radiantChance / total * 100f;
                abyssalChance = abyssalChance / total * 100f;
            }

            // Roll
            float roll = (float)(random.NextDouble() * 100.0);

            if (roll < abyssalChance) return QualityAbyssal;
            if (roll < abyssalChance + radiantChance) return QualityRadiant;
            if (roll < abyssalChance + radiantChance + shimmeringChance) return QualityShimmering;
            return QualityDim;
        }

        /// <summary>
        /// Apply quality multiplier to an attribute value.
        /// Positive attributes are multiplied up, negative attributes are divided (made milder).
        /// </summary>
        public static float ApplyMultiplier(float baseValue, int quality)
        {
            if (!QualityMultipliers.TryGetValue(quality, out float multiplier))
                multiplier = 1.0f;

            if (baseValue >= 0)
            {
                return (float)Math.Round(baseValue * multiplier, 2);
            }
            else
            {
                // Negative attributes become milder (divide absolute value by multiplier)
                return (float)Math.Round(baseValue / multiplier, 2);
            }
        }

        /// <summary>
        /// Get the display color hex for a quality tier.
        /// </summary>
        public static string GetColorHex(int quality)
        {
            return quality switch
            {
                QualityDim => "#9CA3AF",
                QualityShimmering => "#60A5FA",
                QualityRadiant => "#A78BFA",
                QualityAbyssal => "#F59E0B",
                _ => "#9CA3AF"
            };
        }
    }
}
