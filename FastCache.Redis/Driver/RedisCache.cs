using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FastCache.Core.Driver;
using FastCache.Core.Entity;
using NewLife.Caching;
using Newtonsoft.Json;

namespace FastCache.Redis.Driver
{
    public partial class RedisCache : IRedisCache
    {
        private bool _canGetRedisClient = false;

        private readonly FullRedis _redisClient;

        public FullRedis? GetRedisClient()
        {
            return _canGetRedisClient ? _redisClient : null;
        }

        public RedisCache(string connectionString, bool canGetRedisClient = false)
        {
            _canGetRedisClient = canGetRedisClient;
            _redisClient = new FullRedis();
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
            _redisClient.Remove(key);
            return Task.CompletedTask;
        }


        public Task Delete(string key, string prefix = "")
        {
            if (key.Contains('*'))
            {
                string[] list = { };
                if (key.First() == '*' || key.Last() == '*')
                {
                    if (string.IsNullOrEmpty(prefix))
                    {
                        list = _redisClient.Search(key, 1000).ToArray();
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

                        list = string.IsNullOrEmpty(key)
                            ? _redisClient.Search($"{prefix}*", 1000).ToArray()
                            : _redisClient.Search($"{prefix}*", 1000).Where(x => x.Contains(key)).ToArray();
                    }
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
                var removeKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}:{key}";
                _redisClient.Remove(removeKey);
            }

            return Task.CompletedTask;
        }
    }
}