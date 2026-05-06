using System;
using System.Collections.Generic;

namespace VsQuest
{
    /// <summary>
    /// Simple LRU cache with size limit to prevent memory leaks.
    /// Thread-safe implementation using lock synchronization.
    /// </summary>
    public class SimpleLRUCache<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> cache;
        private readonly LinkedList<TKey> lruList;
        private readonly int maxSize;
        private readonly object syncLock = new object();

        public SimpleLRUCache(int maxSize, IEqualityComparer<TKey> comparer = null)
        {
            this.maxSize = maxSize;
            this.cache = new Dictionary<TKey, TValue>(maxSize, comparer);
            this.lruList = new LinkedList<TKey>();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (syncLock)
            {
                if (cache.TryGetValue(key, out value))
                {
                    // Move to front (most recently used)
                    lruList.Remove(key);
                    lruList.AddFirst(key);
                    return true;
                }
                return false;
            }
        }

        public void Add(TKey key, TValue value)
        {
            lock (syncLock)
            {
                if (cache.ContainsKey(key))
                {
                    // Update existing
                    cache[key] = value;
                    lruList.Remove(key);
                    lruList.AddFirst(key);
                }
                else
                {
                    // Evict if at capacity
                    if (cache.Count >= maxSize && lruList.Last != null)
                    {
                        var evictKey = lruList.Last.Value;
                        lruList.RemoveLast();
                        cache.Remove(evictKey);
                    }
                    
                    cache.Add(key, value);
                    lruList.AddFirst(key);
                }
            }
        }

        public bool Remove(TKey key)
        {
            lock (syncLock)
            {
                if (cache.Remove(key))
                {
                    lruList.Remove(key);
                    return true;
                }
                return false;
            }
        }

        public void Clear()
        {
            lock (syncLock)
            {
                cache.Clear();
                lruList.Clear();
            }
        }

        public int Count 
        { 
            get 
            { 
                lock (syncLock) return cache.Count; 
            } 
        }
    }
}
