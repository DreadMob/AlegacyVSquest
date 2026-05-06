using System;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest
{
    /// <summary>
    /// Thread-safe cache for QuestSystem lookups to avoid repeated GetModSystem calls.
    /// </summary>
    public static class QuestSystemCache
    {
        private static readonly ThreadLocal<QuestSystem> CachedInstance = new ThreadLocal<QuestSystem>();
        private static ICoreAPI _api;

        /// <summary>
        /// Initialize the cache with the API. Call once during mod startup.
        /// </summary>
        public static void Initialize(ICoreAPI api)
        {
            _api = api;
            CachedInstance.Value = null;
        }

        /// <summary>
        /// Get QuestSystem instance (cached per thread).
        /// </summary>
        public static QuestSystem Get(ICoreAPI api = null)
        {
            var cached = CachedInstance.Value;
            if (cached != null) return cached;

            var targetApi = api ?? _api;
            if (targetApi == null) return null;

            cached = targetApi.ModLoader.GetModSystem<QuestSystem>();
            if (cached != null)
            {
                CachedInstance.Value = cached;
            }
            return cached;
        }

        /// <summary>
        /// Get QuestSystem from entity's API (convenient overload).
        /// </summary>
        public static QuestSystem GetFromEntity(Entity entity)
        {
            if (entity?.Api == null) return null;
            return Get(entity.Api);
        }

        /// <summary>
        /// Clear the cache (e.g., on mod reload).
        /// </summary>
        public static void Clear()
        {
            CachedInstance.Value = null;
        }
    }
}
