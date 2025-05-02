using System;
using System.Collections.Generic;
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

        Task<DistributedLockResult> ExecuteWithRedisLockAsync(string lockKey,
            Func<Task> operation,
            DistributedLockOptions? options = null,
            CancellationToken cancellationToken = default);

        Task<List<string>> FuzzySearchAsync(
            AdvancedSearchModel advancedSearchModel,
            CancellationToken cancellationToken = default);

        Task<long> BatchDeleteKeysWithPipelineAsync(
            IEnumerable<string> keys,
            int batchSize = 200);
    }
}