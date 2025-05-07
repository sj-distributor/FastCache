using System;
using System.Threading.Tasks;
using FastCache.Core.Entity;

namespace FastCache.Core.Driver
{
    public interface ICacheClient
    {
        Task<bool> Set(string key, CacheItem cacheItem, TimeSpan expire = default);

        Task<CacheItem> Get(string key);

        Task Delete(string key, string prefix);

        Task<bool> Delete(string key);
    }
}