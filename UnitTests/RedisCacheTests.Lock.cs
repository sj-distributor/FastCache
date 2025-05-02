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
        Assert.True(secondClientResult.Status == LockStatus.LockNotAcquired); // 第二个客户端应成功获得锁
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

        Assert.True(distributedLockResult.Status == LockStatus.LockNotAcquired);
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
    /// </summary>
    /// <param name="prefix"></param>
    /// <param name="waitMs"></param>
    /// <param name="operationMs"></param>
    /// <param name="retryMs"></param>
    /// <param name="expectHasLock"></param>
    [Theory]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case0", 100, 220, 200, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case1", 100, 200, 200, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case2", 100, 300, 200, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case3", 100, 400, 200, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case4", 100, 500, 200, 1)]
    [InlineData("LockTimeout_ShouldRespectWaitTime_case5", 500, 200, 200, 2)] 
    public async Task LockTimeout_ShouldRespectWaitTime(string prefix, int waitMs, int operationMs, int retryMs,
        int expectHasLock)
    {
        // Arrange
        var options = new DistributedLockOptions
            { WaitTime = TimeSpan.FromMilliseconds(waitMs), RetryInterval = TimeSpan.FromMilliseconds(retryMs) };

        // Mock长耗时操作
        var operation = async () => await Task.Delay(TimeSpan.FromMilliseconds(operationMs));

        var tasks = new List<Task<DistributedLockResult>>();

        for (int i = 0; i < 2; i++)
        {
            var task = _redisClient.ExecuteWithRedisLockAsync(prefix, operation, options);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        Assert.Equal(expectHasLock, tasks.Count(x => x.Result.Status is LockStatus.AcquiredAndCompleted));
    }
}