using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FastCache.Core.Driver;
using FastCache.Core.Entity;
using Newtonsoft.Json;

namespace FastCache.Redis.Driver
{
    public class RedisCache : IRedisCache
    {
        private bool _canGetRedisClient = false;

        private readonly NewLife.Caching.FullRedis _redisClient;

        public NewLife.Caching.Redis? GetRedisClient()
        {
            return _canGetRedisClient ? _redisClient : null;
        }

        public RedisCache(string connectionString, bool canGetRedisClient = false)
        {
            _canGetRedisClient = canGetRedisClient;
            _redisClient = new NewLife.Caching.FullRedis();
            _redisClient.Init(connectionString);
        }

        public Task Set(string key, CacheItem cacheItem, long expire = 0)
        {
            var hasKey = _redisClient.ContainsKey(key);
            if (hasKey) return Task.CompletedTask;

            if (cacheItem.Value != null)
            {
                cacheItem.Value = JsonConvert.SerializeObject(cacheItem.Value);
            }
            
            if (expire > 0)
            {
                _redisClient.Add(key, cacheItem, (int)expire);
            }
            else
            {
                _redisClient.Add(key, cacheItem);
            }

            return Task.CompletedTask;
        }

        public Task<CacheItem> Get(string key)
        {
            var cacheValue = _redisClient.Get<CacheItem>(key);
            if (cacheValue?.AssemblyName == null || cacheValue?.Type == null) return Task.FromResult(new CacheItem());
            
            var assembly = Assembly.Load(cacheValue.AssemblyName);
            var valueType = assembly.GetType(cacheValue.Type, true, true);
            cacheValue.Value = JsonConvert.DeserializeObject(cacheValue.Value as string, valueType);
            return Task.FromResult(cacheValue);

        }

        public Task Delete(string key)
        {
            if (key.Contains('*'))
            {
                string[] list = { };
                if (key.First() == '*' || key.Last() == '*')
                {
                    list = _redisClient.Search(key, 1000).ToArray();
                }
                else
                {
                    _redisClient.Remove(key);
                }

                if (list?.Length > 0)
                {
                    _redisClient.Remove(list);
                }
            }
            else
            {
                _redisClient.Remove(key);
            }

            return Task.CompletedTask;
        }
    }
}