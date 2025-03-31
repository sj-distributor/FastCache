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
    }
}