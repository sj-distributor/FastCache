using System;
using System.Threading.Tasks;
using NewLife.Caching;

namespace FastCache.Redis.Driver
{
    public partial class RedisCache
    {
        /// <summary>
        /// 使用 Redis 分布式锁来执行操作，包含重试机制（指数退避）
        /// </summary>
        /// <param name="lockKey">锁的键名</param>
        /// <param name="operation">需要执行的异步操作</param>
        /// <param name="retryCount">重试次数，默认为3次</param>
        /// <param name="initialRetryDelayMs">初始重试等待时间（毫秒），默认为100毫秒</param>
        /// <param name="maxRetryDelayMs">最大重试等待时间（毫秒），默认为1000毫秒</param>
        /// <param name="msTimeout">锁的获取超时时间（毫秒），默认为100毫秒</param>
        /// <param name="msExpire">锁的过期时间（毫秒），默认为1000毫秒</param>
        /// <param name="throwOnFailure">失败时是否抛出异常</param>
        /// <returns>操作成功返回 true，否则返回 false</returns>
        public async Task<bool> ExecuteWithRedisLockAsync(string lockKey,
            Func<Task> operation,
            // int retryCount = 3,
            // int initialRetryDelayMs = 100,
            // int maxRetryDelayMs = 1000,
            int msTimeout = 600,
            int msExpire = 3000,
            bool throwOnFailure = false)
        {
            // int currentRetry = 0;
            // int retryDelay = initialRetryDelayMs;

            // while (currentRetry < retryCount)
            // {
            // 尝试获取分布式锁
            using var redisLock = _redisClient.AcquireLock(lockKey, msTimeout, msExpire, throwOnFailure);

            if (redisLock != null)
            {
                try
                {
                    // 执行传入的操作
                    await operation();
                    return true;
                }
                catch (Exception ex)
                {
                    // 记录操作异常
                    // Console.WriteLine($"执行过程中发生错误: {ex.Message}");
                    if (throwOnFailure)
                    {
                        throw;
                    }

                    return false;
                }
            }

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
            if (throwOnFailure)
            {
                throw new InvalidOperationException("Failed to acquire the distributed lock after multiple attempts.");
            }

            return false;
        }
    }
}