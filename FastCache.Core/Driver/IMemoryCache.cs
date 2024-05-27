using System;
using System.Threading.Tasks;
using FastCache.Core.Entity;

namespace FastCache.Core.Driver
{
    public interface IMemoryCache : ICacheClient
    {
        Task SetValue(string key, CacheItem cacheItem, long _ = 0);
        Task<CacheItem> GetValue(string key);
    }
}