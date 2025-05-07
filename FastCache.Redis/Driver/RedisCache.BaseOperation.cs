using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FastCache.Core.Entity;
using FastCache.Core.Extensions;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace FastCache.Redis.Driver
{
    public partial class RedisCache
    {
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

        public async Task<long> BatchDeleteKeysWithPipelineAsync(
            IEnumerable<string> keys,
            int batchSize = 200)
        {
            long totalDeleted = 0;

            var db = _redisConnection.GetDatabase();

            foreach (var chunk in keys.Chunk(batchSize))
            {
                // 1. 创建批处理对象（管道）
                var batch = db.CreateBatch();

                var enumerable = chunk as string[] ?? chunk.ToArray();
                var redisKeys = Array.ConvertAll(enumerable, key => (RedisKey)key);

                var keyDeleteResult = batch.KeyDeleteAsync(redisKeys);

                // 3. 触发批量发送（所有命令一次性发往Redis）
                batch.Execute();

                var resultCount = await keyDeleteResult;

                totalDeleted += resultCount;
            }

            return totalDeleted;
        }

        public async Task Delete(string key, string prefix)
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
                        list = (await FuzzySearchAsync(new AdvancedSearchModel() { Pattern = key, PageSize = 1000 }))
                            .ToArray();
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
                            ? (await FuzzySearchAsync(new AdvancedSearchModel()
                                { Pattern = $"{prefix}*", PageSize = 1000 })).ToArray()
                            : (await FuzzySearchAsync(new AdvancedSearchModel()
                                { Pattern = $"{prefix}*", PageSize = 1000 })).Where(x => x.Contains(key)).ToArray();
                    }
                }
                else
                {
                    await db.KeyDeleteAsync(key);
                }

                if (list?.Length > 0)
                {
                    await BatchDeleteKeysWithPipelineAsync(list);
                }
            }
            else
            {
                var removeKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}:{key}";
                await db.KeyDeleteAsync(removeKey);
            }
        }
    }
}