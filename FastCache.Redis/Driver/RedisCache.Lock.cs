using System;
using System.Threading;
using System.Threading.Tasks;
using FastCache.Core.Entity;
using FastCache.Core.Enums;

namespace FastCache.Redis.Driver
{
    public partial class RedisCache
    {
        /// <summary>
        /// 使用 Redis 分布式锁来执行操作，包含重试机制（指数退避）
        /// </summary>
        /// <param name="lockKey">锁的键名</param>
        /// <param name="operation">需要执行的异步操作</param>
        /// <param name="options">控制参数</param>
        /// <param name="cancellationToken"></param>
        /// <returns>操作成功返回 true，否则返回 false</returns>
        public async Task<DistributedLockResult> ExecuteWithRedisLockAsync(string lockKey,
            Func<Task> operation,
            DistributedLockOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var opts = options ?? new DistributedLockOptions();
            
            await using var redLock = await _redLockFactory
                .CreateLockAsync(lockKey, opts.ExpiryTime, opts.WaitTime, opts.RetryInterval, cancellationToken)
                .ConfigureAwait(false);

            if (!redLock.IsAcquired)
            {
                return new DistributedLockResult
                {
                    IsSuccess = false,
                    Status = LockStatus.LockNotAcquired,
                    Exception = opts.ThrowOnLockFailure
                        ? new InvalidOperationException(
                            "Failed to acquire the distributed lock after multiple attempts")
                        : null
                };
            }

            try
            {
                await operation();
                return new DistributedLockResult
                {
                    IsSuccess = true,
                    Status = LockStatus.AcquiredAndCompleted
                };
            }
            catch (Exception ex)
            {
                if (opts.ThrowOnOperationFailure)
                {
                    throw;
                }

                return new DistributedLockResult
                {
                    IsSuccess = false,
                    Status = LockStatus.OperationFailed,
                    Exception = opts.ThrowOnOperationFailure ? ex : null
                };
            }
        }
    }
}