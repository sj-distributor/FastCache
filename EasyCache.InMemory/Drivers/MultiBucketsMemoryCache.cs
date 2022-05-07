using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using EasyCache.Core.Driver;
using EasyCache.Core.Entity;
using EasyCache.InMemory.Enum;

namespace EasyCache.InMemory.Drivers
{
    public class MultiBucketsMemoryCache : ICacheClient
    {
        private readonly uint _buckets;
        private readonly MaxMemoryPolicy _maxMemoryPolicy;
        private readonly uint _bucketMaxCapacity;
        private static int _cleanupRange;

        private readonly Dictionary<uint, ConcurrentDictionary<string, CacheItem>> _map =
            new Dictionary<uint, ConcurrentDictionary<string, CacheItem>>();

        public MultiBucketsMemoryCache(uint buckets = 5, uint bucketMaxCapacity = 500000,
            MaxMemoryPolicy maxMemoryPolicy = MaxMemoryPolicy.LRU, int cleanUpPercentage = 10)
        {
            if (buckets > 128)
            {
                throw new Exception("buckets must less than 128");
            }

            _buckets = buckets;
            _bucketMaxCapacity = bucketMaxCapacity;
            _maxMemoryPolicy = maxMemoryPolicy;
            _cleanupRange = (int)(bucketMaxCapacity - (bucketMaxCapacity / cleanUpPercentage));
            InitBucket(_map, _buckets);
        }

        public Task Set(string key, CacheItem cacheItem, long _ = 0)
        {
            var bucket = GetBucket(HashKey(key));

            if (bucket.ContainsKey(key)) return Task.CompletedTask;
            if (bucket.Count >= _bucketMaxCapacity)
            {
                ReleaseCached(bucket);
            }

            bucket.TryAdd(key, cacheItem);

            return Task.CompletedTask;
        }

        public Task<CacheItem> Get(string key)
        {
            var bucket = GetBucket(HashKey(key));

            if (!bucket.TryGetValue(key, out var cacheItem)) return Task.FromResult(new CacheItem());
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
                if (key.First() == '*')
                {
                    key = key.Substring(1, key.Length - 1);
                }
                else if (key.Last() == '*')
                {
                    key = key[..^1];
                }

                foreach (var bucket in _map.Keys.Select(bucketId => GetBucket(bucketId)))
                {
                    bucket.Keys.Where(x => x.Contains(key)).ToList().ForEach(k => bucket.TryRemove(k, out var _));
                }
            }
            else
            {
                GetBucket(HashKey(key)).TryRemove(key, out var _);
            }

            return Task.CompletedTask;
        }

        private void InitBucket(Dictionary<uint, ConcurrentDictionary<string, CacheItem>> map, uint buckets)
        {
            for (uint i = 0; i < buckets; i++)
            {
                map.Add(i, new ConcurrentDictionary<string, CacheItem>());
            }
        }

        private ConcurrentDictionary<string, CacheItem> GetBucket(uint bucketId)
        {
            if (_map.TryGetValue(bucketId, out var bucket))
            {
                return bucket;
            }
            
            throw new Exception($"Not Found Bucket: {bucketId}");
        }

        private void ReleaseCached(ConcurrentDictionary<string, CacheItem> bucket)
        {
            var removeRange = bucket.Count - _cleanupRange;

            if (_maxMemoryPolicy == MaxMemoryPolicy.RANDOM)
            {
                foreach (var key in bucket.Keys.TakeLast(removeRange))
                {
                    bucket.Remove(key, out var _);
                }
            }
            else
            {
                IOrderedEnumerable<KeyValuePair<string, CacheItem>> keyValuePairs;
                if (_maxMemoryPolicy == MaxMemoryPolicy.LRU)
                {
                    keyValuePairs = bucket.OrderByDescending(
                        x => x.Value.Hits
                    );
                }
                else
                {
                    keyValuePairs = bucket.OrderByDescending(
                        x => x.Value.CreatedAt
                    );
                }

                foreach (var keyValuePair in keyValuePairs.TakeLast(removeRange))
                {
                    bucket.Remove(keyValuePair.Key, out var _);
                }
            }
        }

        public Dictionary<uint, ConcurrentDictionary<string, CacheItem>> GetBuckets()
        {
            return _map;
        }

        private uint HashKey(string key)
        {
            byte[] encoded = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(key));
            var value = BitConverter.ToUInt32(encoded, 0) % _buckets;
            return value;
        }
    }
}