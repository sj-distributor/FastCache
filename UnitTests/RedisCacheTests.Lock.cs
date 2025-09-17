using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastCache.Core.Entity;
using FastCache.Core.Enums;
using Xunit;

namespace UnitTests;

public partial class RedisCacheTests
{
    private const string TestLockKey = "test_lock";

    // Mock长时间业务操作
    readonly Func<Task> _longRunningOperation = async () =>
    {
        await Task.Delay(TimeSpan.FromSeconds(15)); // >默认expiryTime(30s)
    };

    // Mock快速业务操作  
    private readonly Func<Task> _fastOperation = () => Task.CompletedTask;

    [Fact]
    public async Task SingleClientShouldAcquireLock()
    {
        // Act & Assert (不应抛出异常)
        await _redisClient.ExecuteWithRedisLockAsync(
            $"{TestLockKey}:{nameof(SingleClientShouldAcquireLock)}",
            () => Task.Delay(100), new DistributedLockOptions()
            {
                ThrowOnLockFailure = true
            });
    }

    [Fact]
    public async Task ConcurrentClientsOnlyOneShouldSucceed()
    {
        var successCount = 0;
        var tasks = new List<Task>();

        // Act (模拟5个并发客户端)
        for (var i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var distributedLockResult = await _redisClient.ExecuteWithRedisLockAsync(
                    $"{TestLockKey}:{nameof(ConcurrentClientsOnlyOneShouldSucceed)}", _longRunningOperation);

                if (distributedLockResult is { IsSuccess: true, Status: LockStatus.AcquiredAndCompleted })
                {
                    Interlocked.Increment(ref successCount);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(1, successCount);
    }

    /// <summary>
    /// 验证锁超时其他请求是否能否拿到锁，避免死锁
    /// </summary>
    [Fact]
    public async Task WhenLockExpiresOtherClientsCanAcquireLock()
    {
        // Arrange
        var shortExpiry = TimeSpan.FromSeconds(2);
        var key = $"{TestLockKey}:{nameof(WhenLockExpiresOtherClientsCanAcquireLock)}";

        // Act - 第一个客户端获取锁并故意超时
        var firstClientTask = _redisClient.ExecuteWithRedisLockAsync(
            key,
            () => Task.Delay(3000), // 业务操作超过expiryTime
            new DistributedLockOptions()
            {
                ExpiryTime = shortExpiry,
                ThrowOnLockFailure = false
            }
        );

        // 等待确保第一个客户端已触发锁超时（2秒+缓冲时间）
        await Task.Delay(shortExpiry.Add(TimeSpan.FromSeconds(0.5)));

        // 第二个客户端尝试获取锁（应成功）
        var secondClientResult = await _redisClient.ExecuteWithRedisLockAsync(
            key,
            _fastOperation,
            new DistributedLockOptions()
            {
                WaitTime = TimeSpan.FromSeconds(1)
            }
        );

        // Assert
        Assert.True((await firstClientTask).IsSuccess);
        Assert.Equal(LockStatus.AcquiredAndCompleted, secondClientResult.Status); // 第二个客户端应成功获得锁
    }

    /// <summary>
    /// CancellationToken取消导致获取锁方法取消而获取不到锁
    /// </summary>
    [Fact]
    public async Task CancellationTokenShouldAbortWaiting()
    {
        var successCount = 0;
        var key = $"{TestLockKey}:{nameof(CancellationTokenShouldAbortWaiting)}";
        // Arrange  
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100); //立即取消

        // Act & Assert  
        var distributedLockResult = await _redisClient.ExecuteWithRedisLockAsync(
            key,
            async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
                Interlocked.Increment(ref successCount);
            },
            new DistributedLockOptions()
            {
                WaitTime = TimeSpan.FromSeconds(10),
            },
            cancellationToken: cts.Token);

        Assert.Equal(LockStatus.OperationFailed, distributedLockResult.Status);
        Assert.Equal(0, successCount);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task ThrowOnFailure_ControlsBehavior(bool shouldThrow, bool lockThrowOnOperationFailure)
    {
        Func<Task> lockAction = () =>
        {
            if (shouldThrow) throw new InvalidOperationException("shouldThrow");
            return Task.CompletedTask;
        };

        if (shouldThrow && lockThrowOnOperationFailure)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _redisClient.ExecuteWithRedisLockAsync("force_fail", lockAction, new DistributedLockOptions()
                {
                    ThrowOnOperationFailure = lockThrowOnOperationFailure
                }));
        }

        if (!shouldThrow && lockThrowOnOperationFailure || shouldThrow && !lockThrowOnOperationFailure ||
            !shouldThrow && !lockThrowOnOperationFailure)
        {
            await _redisClient.ExecuteWithRedisLockAsync("force_fail", lockAction, new DistributedLockOptions()
            {
                ThrowOnOperationFailure = lockThrowOnOperationFailure
            });
        }
    }

    /// <summary>
    /// 获取锁超时测试
    /// QuorumRetryCount = 3 redLock 默认值
    /// QuorumRetryDelayMs = 400ms redLock 默认值
    /// 该测试验证red lock在业务逻辑时间极短的情况下内默认配置下系无法锁住的
    /// </summary>
    /// <param name="key"></param>
    /// <param name="waitMs"></param>
    /// <param name="operationMs"></param>
    /// <param name="retryMs"></param>
    /// <param name="expectHasLock"></param>
    [Theory(Skip = "只作为验证red lock分布式锁的实现逻辑的测试")]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case0", 100, 220, 200, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case1", 100, 200, 200, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case2", 100, 300, 200, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case3", 100, 400, 200, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case4", 100, 500, 200, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case5", 500, 200, 200, 2)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case6", 100, 600, 200, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case7", 100, 700, 200, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case8", 0, 200, 200, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case9", 0, 200, 0, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case10", 0, 600, 0, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case11", 0, 700, 0, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case12", 100, 800, 200, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case13", 0, 800, 200, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case14", 0, 800, 0, 1)]
    public async Task LockTimeoutShouldRespectWaitTime(string key, int waitMs, int operationMs, int retryMs,
        int expectHasLock)
    {
        var options = new DistributedLockOptions
            { WaitTime = TimeSpan.FromMilliseconds(waitMs), RetryInterval = TimeSpan.FromMilliseconds(retryMs) };

        // Mock长耗时操作
        var operation = async () => await Task.Delay(TimeSpan.FromMilliseconds(operationMs));

        var tasks = new List<Task<DistributedLockResult>>();

        for (int i = 0; i < 2; i++)
        {
            var task = _redisClient.ExecuteWithRedisLockAsync(key, operation, options);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        Assert.Equal(expectHasLock, tasks.Count(x => x.Result.Status is LockStatus.AcquiredAndCompleted));
    }

    /// <summary>
    /// expectHasLock 只是一个大概的期待值，在线程以及网络抖动下，允许有误差+-1的范围
    /// QuorumRetryCount = 3 redLock 默认值
    /// QuorumRetryDelayMs = 400ms redLock 默认值
    /// </summary>
    /// <param name="key"></param>
    /// <param name="concurrencyLevel"></param>
    /// <param name="waitMs"></param>
    /// <param name="operationMs"></param>
    /// <param name="retryMs"></param>
    /// <param name="expectHasLock"></param>
    [Theory]
    // 常规业务场景 1 等待时间大于业务执行时间，在1000并发模拟请求下大概会有5个成功
    [InlineData(nameof(ExecuteWithRedisLockAsyncWhenConcurrentRequestsShouldRespectLockSemantics) + ":default", 1000,
        500,
        300, 150, 5)]
    // 常规业务场景 2 
    [InlineData(nameof(ExecuteWithRedisLockAsyncWhenConcurrentRequestsShouldRespectLockSemantics) + ":default2", 1000,
        500,
        3000, 150, 1)]
    // 常规业务场景 3 
    [InlineData(nameof(ExecuteWithRedisLockAsyncWhenConcurrentRequestsShouldRespectLockSemantics) + ":default3", 1000,
        500,
        1000, 150, 2)]
    // 常规业务场景 4 
    [InlineData(nameof(ExecuteWithRedisLockAsyncWhenConcurrentRequestsShouldRespectLockSemantics) + ":default4", 1000,
        500,
        1500, 150, 1)]
    // 高竞争场景
    [InlineData(nameof(ExecuteWithRedisLockAsyncWhenConcurrentRequestsShouldRespectLockSemantics) + ":HighContention1",
        5000,
        200,
        100, 50, 10)]
    [InlineData(nameof(ExecuteWithRedisLockAsyncWhenConcurrentRequestsShouldRespectLockSemantics) + ":HighContention2",
        5000,
        200,
        1000, 50, 2)]
    [InlineData(nameof(ExecuteWithRedisLockAsyncWhenConcurrentRequestsShouldRespectLockSemantics) + ":HighContention3",
        5000,
        200,
        1500, 50, 1)]
    [InlineData(nameof(ExecuteWithRedisLockAsyncWhenConcurrentRequestsShouldRespectLockSemantics) + ":HighContention4",
        5000,
        200,
        1200, 50, 1)]
    [InlineData(nameof(ExecuteWithRedisLockAsyncWhenConcurrentRequestsShouldRespectLockSemantics) + ":HighContention5",
        5000,
        500,
        1500, 200, 1)]
    public async Task ExecuteWithRedisLockAsyncWhenConcurrentRequestsShouldRespectLockSemantics(string key,
        int concurrencyLevel, int waitMs, int operationMs, int retryMs,
        int expectHasLock)
    {
        var options = new DistributedLockOptions
            { WaitTime = TimeSpan.FromMilliseconds(waitMs), RetryInterval = TimeSpan.FromMilliseconds(retryMs) };

        // Mock长耗时操作
        var operation = async () => await Task.Delay(TimeSpan.FromMilliseconds(operationMs));

        var tasks = new List<Task<DistributedLockResult>>();

        for (var i = 0; i < concurrencyLevel; i++)
        {
            var task = _redisClient.ExecuteWithRedisLockAsync(key, operation, options);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        var acquiredAndCompletedTaskCount = tasks.Count(x => x.Result.Status is LockStatus.AcquiredAndCompleted);

        testOutputHelper.WriteLine($"""
                                      [RedisLock Test Report]
                                      Scenario: {key}
                                      Concurrency: {concurrencyLevel} requests
                                      Lock Parameters:
                                        - Wait Time: {waitMs}ms
                                        - Operation Time: {operationMs}ms 
                                        - Retry Interval: {retryMs}ms
                                      Results:
                                        - Success Count: {acquiredAndCompletedTaskCount}
                                        - LockNotAcquired Count: {tasks.Count(x => x.Result.Status is LockStatus.LockNotAcquired)}
                                        - Error Count: {tasks.Count(x => x.Result.Exception != null)}
                                    """);

        Assert.True(expectHasLock - 1 == acquiredAndCompletedTaskCount ||
                    expectHasLock + 1 == acquiredAndCompletedTaskCount ||
                    expectHasLock == acquiredAndCompletedTaskCount);
    }

    /// <summary>
    /// expectHasLock 只是一个大概的期待值，在线程以及网络抖动下，允许有误差+-1的范围
    /// QuorumRetryCount = 1
    /// QuorumRetryDelayMs = 400ms redLock 默认值
    /// </summary>
    /// <param name="key"></param>
    /// <param name="concurrencyLevel"></param>
    /// <param name="waitMs"></param>
    /// <param name="operationMs"></param>
    /// <param name="retryMs"></param>
    /// <param name="expectHasLock"></param>
    [Theory]
    // 常规业务场景
    [InlineData(nameof(RedLockWithSingleRetryShouldShowHigherContention) + ":default1", 1000,
        500,
        300, 150, 2)]
    [InlineData(nameof(RedLockWithSingleRetryShouldShowHigherContention) + ":default2", 1000,
        500,
        1000, 150, 1)]
    [InlineData(nameof(RedLockWithSingleRetryShouldShowHigherContention) + ":default3", 1000,
        500,
        800, 150, 1)]
    [InlineData(nameof(RedLockWithSingleRetryShouldShowHigherContention) + ":default4", 1000,
        500,
        600, 150, 1)]
    [InlineData(nameof(RedLockWithSingleRetryShouldShowHigherContention) + ":default5", 1000,
        500,
        500, 150, 1)]
    // 高竞争场景
    [InlineData(nameof(RedLockWithSingleRetryShouldShowHigherContention) + ":HighContention1",
        5000,
        500,
        400, 50, 2)]
    [InlineData(nameof(RedLockWithSingleRetryShouldShowHigherContention) + ":HighContention2",
        5000,
        500,
        600, 50, 1)]
    [InlineData(nameof(RedLockWithSingleRetryShouldShowHigherContention) + ":HighContention3",
        5000,
        500,
        700, 50, 1)]
    [InlineData(nameof(RedLockWithSingleRetryShouldShowHigherContention) + ":HighContention4",
        5000,
        500,
        800, 50, 1)]
    [InlineData(nameof(RedLockWithSingleRetryShouldShowHigherContention) + ":HighContention5",
        5000,
        500,
        900, 50, 1)]
    [InlineData(nameof(RedLockWithSingleRetryShouldShowHigherContention) + ":HighContention6",
        5000,
        500,
        1000, 50, 1)]
    public async Task RedLockWithSingleRetryShouldShowHigherContention(string key,
        int concurrencyLevel, int waitMs, int operationMs, int retryMs,
        int expectHasLock)
    {
        var options = new DistributedLockOptions
            { WaitTime = TimeSpan.FromMilliseconds(waitMs), RetryInterval = TimeSpan.FromMilliseconds(retryMs) };

        // Mock长耗时操作
        var operation = async () => await Task.Delay(TimeSpan.FromMilliseconds(operationMs));

        var tasks = new List<Task<DistributedLockResult>>();

        for (var i = 0; i < concurrencyLevel; i++)
        {
            var task = _redisClient2.ExecuteWithRedisLockAsync(key, operation, options);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        var acquiredAndCompletedTaskCount = tasks.Count(x => x.Result.Status is LockStatus.AcquiredAndCompleted);

        testOutputHelper.WriteLine($"""
                                      [RedisLock Test Report]
                                      Scenario: {key}
                                      Concurrency: {concurrencyLevel} requests
                                      Lock Parameters:
                                        - Wait Time: {waitMs}ms
                                        - Operation Time: {operationMs}ms 
                                        - Retry Interval: {retryMs}ms
                                      Results:
                                        - Success Count: {acquiredAndCompletedTaskCount}
                                        - LockNotAcquired Count: {tasks.Count(x => x.Result.Status is LockStatus.LockNotAcquired)}
                                        - Error Count: {tasks.Count(x => x.Result.Exception != null)}
                                    """);

        Assert.True(expectHasLock - 1 == acquiredAndCompletedTaskCount ||
                    expectHasLock + 1 == acquiredAndCompletedTaskCount ||
                    expectHasLock == acquiredAndCompletedTaskCount);
    }

    /// <summary>
    /// expectHasLock 只是一个大概的期待值，在线程以及网络抖动下，允许有误差+-1的范围
    /// QuorumRetryCount = 1
    /// QuorumRetryDelayMs = 200ms
    /// </summary>
    /// <param name="key"></param>
    /// <param name="concurrencyLevel"></param>
    /// <param name="waitMs"></param>
    /// <param name="operationMs"></param>
    /// <param name="retryMs"></param>
    /// <param name="expectHasLock"></param>
    [Theory]
    // 常规业务场景
    [InlineData(nameof(RedLockWithSingleRetryAndShortDelayShouldShowHighContention) + ":default1", 1000,
        500,
        300, 150, 2)]
    [InlineData(nameof(RedLockWithSingleRetryAndShortDelayShouldShowHighContention) + ":default2", 1000,
        500,
        1000, 150, 1)]
    [InlineData(nameof(RedLockWithSingleRetryAndShortDelayShouldShowHighContention) + ":default3", 1000,
        500,
        800, 150, 1)]
    [InlineData(nameof(RedLockWithSingleRetryAndShortDelayShouldShowHighContention) + ":default4", 1000,
        500,
        600, 150, 1)]
    [InlineData(nameof(RedLockWithSingleRetryAndShortDelayShouldShowHighContention) + ":default5", 1000,
        500,
        500, 150, 1)]
    // 高竞争场景
    [InlineData(nameof(RedLockWithSingleRetryAndShortDelayShouldShowHighContention) + ":HighContention1",
        5000,
        500,
        400, 50, 2)]
    [InlineData(nameof(RedLockWithSingleRetryAndShortDelayShouldShowHighContention) + ":HighContention2",
        5000,
        500,
        600, 50, 1)]
    [InlineData(nameof(RedLockWithSingleRetryAndShortDelayShouldShowHighContention) + ":HighContention3",
        5000,
        500,
        700, 50, 1)]
    [InlineData(nameof(RedLockWithSingleRetryAndShortDelayShouldShowHighContention) + ":HighContention4",
        5000,
        500,
        800, 50, 1)]
    [InlineData(nameof(RedLockWithSingleRetryAndShortDelayShouldShowHighContention) + ":HighContention5",
        5000,
        500,
        900, 50, 1)]
    [InlineData(nameof(RedLockWithSingleRetryAndShortDelayShouldShowHighContention) + ":HighContention6",
        5000,
        500,
        1000, 50, 1)]
    public async Task RedLockWithSingleRetryAndShortDelayShouldShowHighContention(string key,
        int concurrencyLevel, int waitMs, int operationMs, int retryMs,
        int expectHasLock)
    {
        var options = new DistributedLockOptions
            { WaitTime = TimeSpan.FromMilliseconds(waitMs), RetryInterval = TimeSpan.FromMilliseconds(retryMs) };

        // Mock长耗时操作
        var operation = async () => await Task.Delay(TimeSpan.FromMilliseconds(operationMs));

        var tasks = new List<Task<DistributedLockResult>>();

        for (var i = 0; i < concurrencyLevel; i++)
        {
            var task = _redisClient3.ExecuteWithRedisLockAsync(key, operation, options);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        var acquiredAndCompletedTaskCount = tasks.Count(x => x.Result.Status is LockStatus.AcquiredAndCompleted);

        testOutputHelper.WriteLine($"""
                                      [RedisLock Test Report]
                                      Scenario: {key}
                                      Concurrency: {concurrencyLevel} requests
                                      Lock Parameters:
                                        - Wait Time: {waitMs}ms
                                        - Operation Time: {operationMs}ms 
                                        - Retry Interval: {retryMs}ms
                                      Results:
                                        - Success Count: {acquiredAndCompletedTaskCount}
                                        - LockNotAcquired Count: {tasks.Count(x => x.Result.Status is LockStatus.LockNotAcquired)}
                                        - Error Count: {tasks.Count(x => x.Result.Exception != null)}
                                    """);

        Assert.True(expectHasLock - 1 == acquiredAndCompletedTaskCount ||
                    expectHasLock + 1 == acquiredAndCompletedTaskCount ||
                    expectHasLock == acquiredAndCompletedTaskCount);
    }
}