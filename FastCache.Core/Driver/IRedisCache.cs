using System;
using System.Threading.Tasks;

namespace FastCache.Core.Driver
{
    public interface IRedisCache : ICacheClient
    {
        Task<bool> ExecuteWithRedisLockAsync(string lockKey,
            Func<Task> operation,
            int msTimeout = 100,
            int msExpire = 1000,
            bool throwOnFailure = false);
    }
}