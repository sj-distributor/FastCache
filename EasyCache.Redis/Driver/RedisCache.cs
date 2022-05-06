using System.Linq;
using System.Threading.Tasks;
using EasyCache.Core.Driver;
using EasyCache.Core.Entity;
using Newtonsoft.Json;

namespace EasyCache.Redis.Driver
{
    public class RedisCache : ICacheClient
    {
        private bool _canGetRedisClient = false;

        private readonly NewLife.Caching.Redis _redisClient;

        public NewLife.Caching.Redis? GetRedisClient()
        {
            return _canGetRedisClient ? _redisClient : null;
        }

        public RedisCache(string connectionString, bool canGetRedisClient = false)
        {
            _canGetRedisClient = canGetRedisClient;
            _redisClient = new NewLife.Caching.Redis();
            _redisClient.Init(connectionString);
        }

        public Task Set(string key, CacheItem cacheItem, long expire = 0)
        {
            var hasKey = _redisClient.ContainsKey(key);
            if (hasKey) return Task.CompletedTask;
            if (expire > 0)
            {
                _redisClient.Add(key, JsonConvert.SerializeObject(cacheItem), (int) expire);
            }
            else
            {
                _redisClient.Add(key, JsonConvert.SerializeObject(cacheItem));
            }

            return Task.CompletedTask;
        }

        public Task<CacheItem> Get(string key)
        {
            var result = _redisClient.Get<CacheItem>(key);
            return Task.FromResult(result ?? new CacheItem());
        }

        public Task Delete(string key)
        {
            if (key.Contains('*'))
            {
                if (key.First() == '*')
                {
                    key = key.Substring(1, key.Length);
                }
                else if (key.Last() == '*')
                {
                    key = key[..^1];
                }

                var list = _redisClient.Keys.Where(x => x.Contains(key)).ToArray();
                _redisClient.Remove(list);
            }
            else
            {
                _redisClient.Remove(key);
            }

            return Task.CompletedTask;
        }
    }
}