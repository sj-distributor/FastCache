using System.Threading.Tasks;
using EasyCache.Core.Entity;

namespace EasyCache.Core.Driver
{
    public interface ICacheClient
    {
        Task Set(string key, CacheItem cacheItem, long expire = 0);

        Task<CacheItem> Get(string key);

        Task Delete(string key);
    }
}