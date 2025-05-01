using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FastCache.Core.Driver;
using FastCache.Core.Entity;
using Newtonsoft.Json;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;

namespace FastCache.Redis.Driver
{
    public partial class RedisCache : IRedisCache
    {
        // private bool _canGetRedisClient = false;

        // private readonly FullRedis _redisClient;

        private RedLockFactory _redLockFactory;

        private ConnectionMultiplexer _redisConnection;

        // public FullRedis? GetRedisClient()
        // {
        //     return _canGetRedisClient ? _redisClient : null;
        // }

        public ConnectionMultiplexer GetConnectionMultiplexer()
        {
            return _redisConnection;
        }

        public RedLockFactory GetRedLockFactory()
        {
            return _redLockFactory;
        }

        public RedisCache(string connectionString, bool canGetRedisClient = false)
        {
            // _canGetRedisClient = canGetRedisClient;
            // _redisClient = new FullRedis();
            // _redisClient.Init(connectionString);

            _redisConnection = ConnectionMultiplexer.Connect(connectionString);

            if (_redisConnection == null)
                throw new InvalidOperationException();

            SetupRedisLockFactory(new List<ConnectionMultiplexer>() { _redisConnection });
        }

        private void SetupRedisLockFactory(List<ConnectionMultiplexer> connectionMultiplexers)
        {
            var redLockMultiplexers = connectionMultiplexers
                .Select(connectionMultiplexer => (RedLockMultiplexer)connectionMultiplexer).ToList();

            _redLockFactory = RedLockFactory.Create(redLockMultiplexers);
        }

        public async Task<bool> Set(string key, CacheItem cacheItem, TimeSpan expire = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            if (cacheItem == null)
                throw new ArgumentNullException(nameof(cacheItem));

            var db = _redisConnection.GetDatabase();

            var hasKey = db.KeyExists(key);

            if (hasKey) return true;

            if (cacheItem.Value != null)
            {
                cacheItem.Value = JsonConvert.SerializeObject(cacheItem.Value);
            }

            var value = JsonConvert.SerializeObject(cacheItem);

            if (expire != default)
            {
                return await db.StringSetAsync(key, value: value, expiry: expire, when: When.NotExists)
                    .ConfigureAwait(false);
            }

            return await db.StringSetAsync(key, value: value, when: When.NotExists).ConfigureAwait(false);
        }

        public async Task<CacheItem> Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            var db = _redisConnection.GetDatabase();

            var cache = await db.StringGetAsync(key);

            if (cache.IsNullOrEmpty) return new CacheItem();

            var cacheItem = JsonConvert.DeserializeObject<CacheItem>(cache);

            if (string.IsNullOrWhiteSpace(cacheItem?.AssemblyName) || string.IsNullOrWhiteSpace(cacheItem?.Type) ||
                cacheItem?.Value == null) return new CacheItem();

            var assembly = Assembly.Load(cacheItem.AssemblyName);
            var valueType = assembly.GetType(cacheItem.Type, true, true);
            cacheItem.Value = JsonConvert.DeserializeObject(cacheItem.Value as string, valueType);
            return cacheItem;
        }

        public Task<bool> Delete(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            var db = _redisConnection.GetDatabase();

            db.KeyDelete(key);

            return Task.FromResult(true);
        }


        public Task Delete(string key, string prefix)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            
            var db = _redisConnection.GetDatabase();
            
            if (key.Contains('*'))
            {
                string[] list = { };
                if (key.First() == '*' || key.Last() == '*')
                {
                    if (string.IsNullOrEmpty(prefix))
                    {
                        // list = _redisClient.Search(key, 1000).ToArray();
                    }
                    else
                    {
                        if (key.Length > 0 && key.First() == '*')
                        {
                            key = key[1..];
                        }

                        if (key.Length > 0 && key.Last() == '*')
                        {
                            key = key[..^1];
                        }

                        // list = string.IsNullOrEmpty(key)
                        //     ? _redisClient.Search($"{prefix}*", 1000).ToArray()
                        //     : _redisClient.Search($"{prefix}*", 1000).Where(x => x.Contains(key)).ToArray();
                    }
                }
                else
                {
                    // _redisClient.Remove(key);
                }

                if (list?.Length > 0)
                {
                    // _redisClient.Remove(list);
                }
            }
            else
            {
                var removeKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}:{key}";
                // _redisClient.Remove(removeKey);
            }

            return Task.CompletedTask;
        }
    }
}