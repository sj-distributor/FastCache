using System;
using System.Threading;
using System.Threading.Tasks;
using FastCache.Core.Entity;
using RedLockNet.SERedis;
using StackExchange.Redis;

namespace FastCache.Core.Driver
{
    public interface IRedisCache : ICacheClient
    {
        ConnectionMultiplexer GetConnectionMultiplexer();

        RedLockFactory GetRedLockFactory();

        Task<bool> ExecuteWithRedisLockAsync(string lockKey,
            Func<Task> operation,
            TimeSpan expiryTime = default,
            TimeSpan waitTime = default,
            TimeSpan retryTime = default,
            bool throwOnFailure = false,
            CancellationToken cancellationToken = default);
    }
}