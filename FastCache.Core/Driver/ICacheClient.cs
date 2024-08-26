using System.Threading.Tasks;
using FastCache.Core.Entity;

namespace FastCache.Core.Driver
{
    public interface ICacheClient
    {
        Task Set(string key, CacheItem cacheItem, long expire = 0);

        Task<CacheItem> Get(string key);

        Task Delete(string key, string prefix);

        Task Delete(string key);

        Task<bool> SetAsyncLock(string key, CacheItem cacheItem, long expire = 0,
            int msTimeout = 100,
            int msExpire = 1000,
            bool throwOnFailure = false);

        Task<bool> DeleteAsyncLock(string key, string prefix = "",
            int msTimeout = 100,
            int msExpire = 1000,
            bool throwOnFailure = false);
    }
}