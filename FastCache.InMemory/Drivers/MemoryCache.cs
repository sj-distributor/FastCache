using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FastCache.Core.Driver;
using FastCache.Core.Entity;
using FastCache.InMemory.Enum;

namespace FastCache.InMemory.Drivers
{
    public class MemoryCache : IMemoryCache
    {
        private readonly int _maxCapacity;
        private readonly MaxMemoryPolicy _maxMemoryPolicy;
        private static int _cleanupRange;

        private static ConcurrentDictionary<string, CacheItem> _dist = null!;

        public MemoryCache(int maxCapacity = 5000000, MaxMemoryPolicy maxMemoryPolicy = MaxMemoryPolicy.LRU,
            int cleanUpPercentage = 10)
        {
            _maxCapacity = maxCapacity;
            _maxMemoryPolicy = maxMemoryPolicy;
            _cleanupRange = _maxCapacity - (_maxCapacity / cleanUpPercentage);
            _dist = new ConcurrentDictionary<string, CacheItem>(Environment.ProcessorCount * 2, _maxCapacity);
        }

        public Task Set(string key, CacheItem cacheItem, long _ = 0)
        {
            if (_dist.ContainsKey(key)) return Task.CompletedTask;
            if (_dist.Count >= _maxCapacity)
            {
                ReleaseCached();
            }

            _dist.TryAdd(key, cacheItem);

            return Task.CompletedTask;
        }

        public Task<CacheItem> Get(string key)
        {
            if (!_dist.TryGetValue(key, out var cacheItem)) return Task.FromResult(new CacheItem());

            if (cacheItem.Expire < DateTime.Now.Ticks)
            {
                Delete(key);
                return Task.FromResult(new CacheItem());
            }

            ++cacheItem.Hits;

            return Task.FromResult(cacheItem);
        }

        public Task Delete(string key)
        {
            if (key.Contains('*'))
            {
                if (key.Length > 0 && key.First() == '*')
                {
                    key = key[1..];
                }

                if (key.Length > 0 && key.Last() == '*')
                {
                    key = key[..^1];
                }

                if (string.IsNullOrEmpty(key))
                {
                    _dist.Keys.ToList().ForEach(k => _dist.TryRemove(k, out var _));
                }
                else
                {
                    _dist.Keys.Where(x => x.Contains(key)).ToList().ForEach(k => _dist.TryRemove(k, out var _));
                }
            }
            else
            {
                _dist.TryRemove(key, out var _);
            }

            return Task.CompletedTask;
        }

        private void ReleaseCached()
        {
            if (_dist.Count < _maxCapacity) return;

            var removeRange = _dist.Count - _cleanupRange;

            if (_maxMemoryPolicy == MaxMemoryPolicy.RANDOM)
            {
                foreach (var key in _dist.Keys.TakeLast(removeRange))
                {
                    _dist.Remove(key, out var _);
                }
            }
            else
            {
                IOrderedEnumerable<KeyValuePair<string, CacheItem>> keyValuePairs;
                if (_maxMemoryPolicy == MaxMemoryPolicy.LRU)
                {
                    keyValuePairs = _dist.OrderByDescending(
                        x => x.Value.Hits
                    );
                }
                else
                {
                    keyValuePairs = _dist.OrderByDescending(
                        x => x.Value.CreatedAt
                    );
                }

                foreach (var keyValuePair in keyValuePairs.TakeLast(removeRange))
                {
                    _dist.Remove(keyValuePair.Key, out var _);
                }
            }
        }

        public ConcurrentDictionary<string, CacheItem> GetBuckets()
        {
            return _dist;
        }
    }
}