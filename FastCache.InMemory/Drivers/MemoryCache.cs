using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FastCache.Core.Driver;
using FastCache.Core.Entity;
using FastCache.InMemory.Enum;
using FastCache.InMemory.Extension;
using Newtonsoft.Json;

namespace FastCache.InMemory.Drivers
{
    public class MemoryCache : IMemoryCache
    {
        private readonly int _maxCapacity;
        private readonly MaxMemoryPolicy _maxMemoryPolicy;
        private static int _cleanupRange;
        private readonly int _delaySeconds;

        private static ConcurrentDictionary<string, CacheItem> _dist = null!;

        public MemoryCache(int maxCapacity = 5000000, MaxMemoryPolicy maxMemoryPolicy = MaxMemoryPolicy.LRU,
            int cleanUpPercentage = 10, int delaySeconds = 2)
        {
            _delaySeconds = delaySeconds;
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

            cacheItem.Value = JsonConvert.SerializeObject(cacheItem.Value);
            _dist.TryAdd(key, cacheItem);

            return Task.CompletedTask;
        }

        public Task<CacheItem> Get(string key)
        {
            if (!_dist.TryGetValue(key, out var cacheItem)) return Task.FromResult(new CacheItem());

            if (cacheItem.Expire < DateTime.UtcNow.Ticks)
            {
                Delete(key);
                return Task.FromResult(new CacheItem());
            }
            if (cacheItem?.AssemblyName == null || cacheItem?.Type == null) return Task.FromResult(new CacheItem());
            ++cacheItem.Hits;
            var assembly = Assembly.Load(cacheItem.AssemblyName);
            var valueType = assembly.GetType(cacheItem.Type, true, true);
            cacheItem.Value = JsonConvert.DeserializeObject(cacheItem.Value as string, valueType);
            return Task.FromResult(cacheItem);
        }

        public Task Delete(string key, string prefix = "")
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

                var queryList = string.IsNullOrEmpty(prefix) ? _dist.Keys : _dist.Keys.Where(x => x.Contains(prefix));

                if (string.IsNullOrEmpty(key))
                {
                    queryList.ToList().ForEach(k => _dist.TryRemove(k, out _, _delaySeconds));
                }
                else
                {
                    queryList.Where(x => x.Contains(key)).ToList()
                        .ForEach(k => _dist.TryRemove(k, out _, _delaySeconds));
                }
            }
            else
            {
                var removeKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}:{key}";

                _dist.TryRemove(removeKey, out _, _delaySeconds);
            }

            return Task.CompletedTask;
        }

        public Task Delete(string key)
        {
            _dist.TryRemove(key, out _, _delaySeconds);
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
                    _dist.TryRemove(key, out _, _delaySeconds);
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
                    _dist.TryRemove(keyValuePair.Key, out _, _delaySeconds);
                }
            }
        }

        public ConcurrentDictionary<string, CacheItem> GetBuckets()
        {
            return _dist;
        }
    }
}