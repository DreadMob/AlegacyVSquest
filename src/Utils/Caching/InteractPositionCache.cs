using System;
using Vintagestory.API.Common;

namespace VsQuest
{
    public interface IInteractPositionCache
    {
        void UpdatePosition(string questId, int[] position);
    }

    public class InteractPositionCache : IInteractPositionCache
    {
        private const int LastInteractDebounceMs = 100;

        private class LastInteractCache
        {
            public long LastWriteMs;
            public int X;
            public int Y;
            public int Z;
        }

        private readonly SimpleLRUCache<string, LastInteractCache> _cache = new SimpleLRUCache<string, LastInteractCache>(100);

        public void UpdatePosition(string questId, int[] position)
        {
            if (string.IsNullOrEmpty(questId) || position == null || position.Length < 3) return;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (!_cache.TryGetValue(questId, out var cache))
            {
                _cache.Add(questId, new LastInteractCache
                {
                    LastWriteMs = now,
                    X = position[0],
                    Y = position[1],
                    Z = position[2]
                });
                return;
            }

            // Only update if enough time has passed
            if (now - cache.LastWriteMs >= LastInteractDebounceMs)
            {
                cache.LastWriteMs = now;
                cache.X = position[0];
                cache.Y = position[1];
                cache.Z = position[2];
            }
        }
    }
}
