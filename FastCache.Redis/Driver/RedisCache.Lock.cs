using System;
using System.Threading;
using System.Threading.Tasks;

namespace FastCache.Redis.Driver
{
    public partial class RedisCache
    {
        /// <summary>
        /// 使用 Redis 分布式锁来执行操作，包含重试机制（指数退避）
        /// </summary>
        /// <param name="lockKey">锁的键名</param>
        /// <param name="operation">需要执行的异步操作</param>
        /// <param name="retryTime"></param>
        /// <param name="throwOnFailure">失败时是否抛出异常</param>
        /// <param name="expiryTime"></param>
        /// <param name="waitTime"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>操作成功返回 true，否则返回 false</returns>
        public async Task<bool> ExecuteWithRedisLockAsync(string lockKey,
            Func<Task> operation,
            TimeSpan expiryTime = default,
            TimeSpan waitTime = default,
            TimeSpan retryTime = default,
            bool throwOnFailure = false,
            CancellationToken cancellationToken = default)
        {
            // 设置合理的默认值（如果未显式指定）
            // expiryTime	30秒	- 足够覆盖大多数业务操作<br>- Redis官方推荐锁有效期应大于业务执行时间
            // waitTime	10秒	- 平衡用户体验和系统资源<br>- 超过该时间可认为系统繁忙
            // retryTime	200毫秒	- Redis性能考量（网络往返+命令执行）<br>- AWS建议的退避基准值
            expiryTime = expiryTime == default ? TimeSpan.FromSeconds(30) : expiryTime;
            waitTime = waitTime == default ? TimeSpan.FromSeconds(10) : waitTime;
            retryTime = retryTime == default ? TimeSpan.FromMilliseconds(200) : retryTime;

            await using var redLock = await _redLockFactory
                .CreateLockAsync(lockKey, expiryTime, waitTime, retryTime, cancellationToken)
                .ConfigureAwait(false);

            if (!redLock.IsAcquired)
            {
                if (throwOnFailure)
                {
                    throw new InvalidOperationException(
                        "Failed to acquire the distributed lock after multiple attempts.");
                }

                return false;
            }

            try
            {
                await operation();
                return true;
            }
            catch (Exception _)
            {
                if (throwOnFailure)
                {
                    throw;
                }

                return false;
            }


            // 尝试获取分布式锁
            // using var redisLock = _redisClient.AcquireLock(lockKey, msTimeout, msExpire, throwOnFailure);
            //
            // if (redisLock != null)
            // {
            //     try
            //     {
            //         // 执行传入的操作
            //         await operation();
            //         return true;
            //     }
            //     catch (Exception ex)
            //     {
            //         // 记录操作异常
            //         // Console.WriteLine($"执行过程中发生错误: {ex.Message}");
            //         if (throwOnFailure)
            //         {
            //             throw;
            //         }
            //
            //         return false;
            //     }
            // }

            //     currentRetry++;
            //
            //     if (currentRetry < retryCount)
            //     {
            //         // 指数退避策略：每次重试后增加等待时间
            //         retryDelay = Math.Min(retryDelay * 2, maxRetryDelayMs);
            //         await Task.Delay(retryDelay);
            //     }
            // }

            // 如果在所有重试中都未获取锁，则返回失败
            // if (throwOnFailure)
            // {
            //     throw new InvalidOperationException("Failed to acquire the distributed lock after multiple attempts.");
            // }

            // return false;
        }
    }
}