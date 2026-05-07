using System;
using System.Collections.Generic;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Handles quest selection logic for quest givers, including rotation,
    /// random selection, boss hunt integration, and exclusion filtering.
    /// </summary>
    public class QuestSelectionService
    {
        private readonly string[] quests;
        private readonly string[] alwaysQuests;
        private readonly string[] rotationPool;
        private readonly string[] excludeQuests;
        private readonly string[] excludeQuestPrefixes;
        private readonly bool selectRandom;
        private readonly int selectRandomCount;
        private readonly int rotationDays;
        private readonly int rotationCount;
        private readonly bool allQuests;
        private readonly bool bossHuntActiveOnly;
        private readonly long entityId;

        /// <summary>
        /// Creates a new QuestSelectionService with the specified configuration.
        /// </summary>
        public QuestSelectionService(
            string[] quests,
            string[] alwaysQuests,
            string[] rotationPool,
            string[] excludeQuests,
            string[] excludeQuestPrefixes,
            bool selectRandom,
            int selectRandomCount,
            int rotationDays,
            int rotationCount,
            bool allQuests,
            bool bossHuntActiveOnly,
            long entityId)
        {
            this.quests = quests ?? Array.Empty<string>();
            this.alwaysQuests = alwaysQuests ?? Array.Empty<string>();
            this.rotationPool = rotationPool;
            this.excludeQuests = excludeQuests ?? Array.Empty<string>();
            this.excludeQuestPrefixes = excludeQuestPrefixes ?? Array.Empty<string>();
            this.selectRandom = selectRandom;
            this.selectRandomCount = selectRandomCount;
            this.rotationDays = rotationDays;
            this.rotationCount = rotationCount;
            this.allQuests = allQuests;
            this.bossHuntActiveOnly = bossHuntActiveOnly;
            this.entityId = entityId;
        }

        /// <summary>
        /// Checks if a quest is currently relevant for this quest giver.
        /// </summary>
        public bool IsQuestCurrentlyRelevant(ICoreServerAPI sapi, string questId, IReadOnlyDictionary<string, Quest> questRegistry)
        {
            if (string.IsNullOrWhiteSpace(questId)) return false;

            var selection = allQuests
                ? (IEnumerable<string>)BuildAllQuestIds(questRegistry)
                : GetCurrentQuestSelection(sapi);

            foreach (var q in selection)
            {
                if (string.Equals(q, questId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Builds a set of all quest IDs available to this quest giver (excluding excluded quests).
        /// </summary>
        public HashSet<string> BuildAllQuestIds(IReadOnlyDictionary<string, Quest> questRegistry)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (allQuests && questRegistry != null)
            {
                foreach (var qid in questRegistry.Keys)
                {
                    if (!IsExcluded(qid)) set.Add(qid);
                }
            }
            else
            {
                foreach (var q in quests) if (!IsExcluded(q)) set.Add(q);
                foreach (var q in alwaysQuests) if (!IsExcluded(q)) set.Add(q);
                if (rotationPool != null)
                {
                    foreach (var q in rotationPool) if (!IsExcluded(q)) set.Add(q);
                }
            }
            return set;
        }

        /// <summary>
        /// Gets the current quest selection based on configuration (rotation, boss hunt, etc.).
        /// </summary>
        public List<string> GetCurrentQuestSelection(ICoreServerAPI sapi)
        {
            var result = new List<string>();

            // Always quests are always included
            foreach (var q in alwaysQuests)
            {
                if (!IsExcluded(q)) result.Add(q);
            }

            // Boss hunt mode: only return the active boss quest
            if (bossHuntActiveOnly)
            {
                var bossSystem = sapi?.ModLoader?.GetModSystem<BossHuntSystem>();
                var activeQuestId = bossSystem?.GetActiveBossQuestId();
                if (!string.IsNullOrWhiteSpace(activeQuestId) && !IsExcluded(activeQuestId) && !result.Contains(activeQuestId))
                {
                    result.Add(activeQuestId);
                }
                return result;
            }

            // Determine the pool to use
            var pool = rotationPool ?? quests;
            if (pool == null || pool.Length == 0)
            {
                return result;
            }

            // No rotation: return entire pool
            if (rotationDays <= 0 || sapi == null)
            {
                foreach (var q in pool)
                {
                    if (!IsExcluded(q)) result.Add(q);
                }
                return result;
            }

            // Rotation logic: deterministic order based on time and entity ID
            int period = (int)Math.Floor(sapi.World.Calendar.TotalDays / rotationDays);
            int offset = Math.Abs(unchecked((int)entityId));
            offset = pool.Length == 0 ? 0 : offset % pool.Length;

            // Include entire pool in rotated order (eligibility checked later)
            for (int i = 0; i < pool.Length; i++)
            {
                int idx = (offset + period + i) % pool.Length;
                string questId = pool[idx];
                if (!IsExcluded(questId) && !result.Contains(questId))
                {
                    result.Add(questId);
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if a quest is in the exclusion list.
        /// </summary>
        public bool IsExcluded(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return true;

            for (int i = 0; i < excludeQuests.Length; i++)
            {
                var q = excludeQuests[i];
                if (string.IsNullOrWhiteSpace(q)) continue;
                if (string.Equals(q, questId, StringComparison.OrdinalIgnoreCase)) return true;
            }

            for (int i = 0; i < excludeQuestPrefixes.Length; i++)
            {
                var p = excludeQuestPrefixes[i];
                if (string.IsNullOrWhiteSpace(p)) continue;
                if (questId.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the number of days until the next rotation.
        /// </summary>
        public int GetRotationDaysLeft(ICoreServerAPI sapi)
        {
            if (bossHuntActiveOnly)
            {
                var bossSystem = sapi?.ModLoader?.GetModSystem<BossHuntSystem>();
                if (bossSystem != null && bossSystem.TryGetBossHuntStatus(out _, out _, out double hoursUntilRotation))
                {
                    if (hoursUntilRotation > 0)
                    {
                        int days = (int)Math.Ceiling(hoursUntilRotation / 24.0);
                        return Math.Max(0, days);
                    }
                }
                return 0;
            }

            if (rotationDays <= 0 || sapi == null) return 0;

            try
            {
                double nowDays = sapi.World.Calendar.TotalDays;
                double period = Math.Floor(nowDays / rotationDays);
                double nextRotationDay = (period + 1) * rotationDays;
                double leftDays = nextRotationDay - nowDays;
                if (!double.IsNaN(leftDays) && !double.IsInfinity(leftDays) && leftDays > 0)
                {
                    int days = (int)Math.Ceiling(leftDays);
                    return Math.Max(0, days);
                }
            }
            catch
            {
                // Ignore calendar errors
            }

            return 0;
        }

        /// <summary>
        /// Gets the maximum number of quests that should be offered.
        /// </summary>
        public int GetOfferLimit()
        {
            if (rotationDays > 0) return Math.Max(1, rotationCount);
            return int.MaxValue;
        }
    }
}
