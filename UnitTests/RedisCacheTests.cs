using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FastCache.Core.Entity;
using FastCache.Redis.Constant;
using FastCache.Redis.Driver;
using NewLife.Log;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests;

public class RedisCacheTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    private readonly RedisCache _redisClient =
        new("server=localhost:6379;timeout=5000;MaxMessageSize=1024000;Expire=3600", true);

    public RedisCacheTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("anson", "18", "18")]
    [InlineData("anson1", "19", "19")]
    public async void TestRedisCacheCanSet(string key, string value, string result)
    {
        await _redisClient.Set(key, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        }, 20);
        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("key1", "18", null, 1)]
    [InlineData("key2", "19", null, 1)]
    public async void TestRedisCacheCanSetTimeout(string key, string value, string result, long expire = 0)
    {
        await _redisClient.Set(key, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        }, expire);

        await Task.Delay(TimeSpan.FromSeconds(3));

        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("anson", "18", null)]
    [InlineData("anson1", "19", null)]
    public async void TestRedisCacheCanDelete(string key, string value, string result)
    {
        await _redisClient.Set(key, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        });
        await _redisClient.Delete(key);
        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("", "anson1111", "18", null)]
    [InlineData("", "anson2222", "19", null)]
    public async void TestRedisCacheCanDeleteByLastPattern(string prefix, string key, string value, string result)
    {
        await _redisClient.Set(key, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        });
        await _redisClient.Delete("anson*", prefix);
        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("", "anson1111", "18", null)]
    [InlineData("", "anson2222", "19", null)]
    public async void TestRedisCacheCanDeleteByLastPatternByFullKey(string prefix, string key, string value,
        string result)
    {
        await _redisClient.Set(key, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        });
        await _redisClient.Delete(key);
        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("anson", "anson1111", "18", null)]
    [InlineData("anson", "anson2222", "19", null)]
    public async void TestRedisCacheCanDeleteByLastPatternWithPrefix(string prefix, string key, string value,
        string result)
    {
        var fullKey = $"{prefix}:{key}";
        await _redisClient.Set(fullKey, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        });
        await _redisClient.Delete("anson*", prefix);
        var s = await _redisClient.Get(fullKey);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("anson", "anson1111", "18", null)]
    [InlineData("anson", "anson2222", "19", null)]
    public async void TestRedisCacheCanDeleteByLastPatternWithPrefixByFullKey(string prefix, string key, string value,
        string result)
    {
        var fullKey = $"{prefix}:{key}";
        await _redisClient.Set(fullKey, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        });
        await _redisClient.Delete(fullKey);
        var s = await _redisClient.Get(fullKey);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("", "1111Joe", "18", null)]
    [InlineData("", "2222Joe", "19", null)]
    public async void TestRedisCacheCanDeleteByFirstPattern(string prefix, string key, string value, string result)
    {
        await _redisClient.Set(key, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        });
        await _redisClient.Delete("*Joe", prefix);
        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("Joe", "1111Joe", "18", null)]
    [InlineData("Joe", "2222Joe", "19", null)]
    public async void TestRedisCacheCanDeleteByFirstPatternWithPrefix(string prefix, string key, string value,
        string result)
    {
        var fullKey = $"{prefix}:{key}";
        await _redisClient.Set(fullKey, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        });
        await _redisClient.Delete("*Joe", prefix);
        var s = await _redisClient.Get(fullKey);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("", "1111Joe", "*Joe*", "18", null)]
    [InlineData("", "2222Joe22222", "*Joe*", "19", null)]
    [InlineData("", "3333Joe22222", "*Joe*", "20", null)]
    public async void TestRedisCacheCanDeleteByFirstAndLastPattern(string prefix, string key, string deleteKey,
        string value, string result)
    {
        await _redisClient.Set(key, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        });
        await _redisClient.Delete(deleteKey, prefix);
        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("Joe", "1111Joe", "*Joe*", "18", null)]
    [InlineData("Joe", "2222Joe22222", "*Joe*", "19", null)]
    [InlineData("Joe", "3333Joe22222", "*Joe*", "20", null)]
    public async void TestRedisCacheCanDeleteByFirstAndLastPatternWithPrefix(string prefix, string key,
        string deleteKey, string value, string result)
    {
        var fullKey = $"{prefix}:{key}";
        await _redisClient.Set(fullKey, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        });
        await _redisClient.Delete(deleteKey, prefix);
        var s = await _redisClient.Get(fullKey);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("", "1111Joe", "*", "18", null)]
    [InlineData("", "2222Joe22222", "*", "19", null)]
    [InlineData("", "3333Joe22222", "*", "20", null)]
    public async void TestRedisCacheCanDeleteByGeneralMatchPattern(string prefix, string key, string deleteKey,
        string value, string result)
    {
        await _redisClient.Set(key, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        });
        await _redisClient.Delete(deleteKey, prefix);
        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("Joe", "1111Joe", "*", "18", null)]
    [InlineData("Joe", "2222Joe22222", "*", "19", null)]
    [InlineData("Joe", "3333Joe22222", "*", "20", null)]
    public async void TestRedisCacheCanDeleteByGeneralMatchPatternWithPrefix(string prefix, string key,
        string deleteKey, string value, string result)
    {
        var fullKey = $"{prefix}:{key}";
        await _redisClient.Set(fullKey, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        });
        await _redisClient.Delete(deleteKey, prefix);
        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("Joe", "1111Joe", "*", "18", null)]
    [InlineData("Joe", "2222Joe22222", "*", "19", null)]
    [InlineData("Joe", "3333Joe22222", "*", "20", null)]
    public async void TestLockRedisCacheCanDeleteByGeneralMatchPatternWithPrefix(string prefix, string key,
        string deleteKey, string value, string result)
    {
        var fullKey = $"{prefix}:{key}";
        await _redisClient.SetAsyncLock(fullKey, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        });
        await _redisClient.DeleteAsyncLock(deleteKey, prefix);
        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }

    /// <summary>
    /// 测试 Redis 缓存添加方法在不同延迟和线性请求超时情况下的分布式锁获取情况。
    /// 该测试模拟了多个客户端争抢锁的情况，每个客户端请求之前有不同的延迟时间，
    /// 以测试锁机制的有效性，以及在延迟和锁超时的情况下有多少个请求可以成功获得锁。
    /// </summary>
    /// <param name="prefix"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="delayMs"></param>
    /// <param name="expect"></param>
    [Theory]
    [InlineData("Joe", "1111Joe", "88888888888888888888888888888888888888888888888888888888888888888", 100, 5)]
    [InlineData("Joe", "1111Joe", "88888888888888888888888888888888888888888888888888888888888888888", 200, 4)]
    [InlineData("Joe", "1111Joe", "88888888888888888888888888888888888888888888888888888888888888888", 300, 3)]
    [InlineData("Joe", "1111Joe", "88888888888888888888888888888888888888888888888888888888888888888", 700, 2)]
    [InlineData("Joe", "1111Joe", "88888888888888888888888888888888888888888888888888888888888888888", 900, 1)]
    public async void TestLockRedisCacheAddWithVaryingDelaysAndLinearTimeoutHandling(string prefix, string key,
        string value,
        int delayMs, int expect)
    {
        var fullKey = $"{prefix}:{key}";

        var client = _redisClient.GetRedisClient()!;

        var lockResult = new ConcurrentDictionary<string, bool>();

        // 定义一个异步操作，用于模拟不同客户端争抢锁
        async Task<bool> TrySetAsyncLock(string v)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var isLock = await _redisClient.ExecuteWithRedisLockAsync(client,
                $"{Prefix.DeletePrefix}:{fullKey}", async () =>
                {
                    await Task.Delay(delayMs);
                    await _redisClient.Set(fullKey, new CacheItem()
                    {
                        Value = $"{value}-{v}",
                        AssemblyName = value.GetType().Assembly.GetName().Name,
                        Type = value.GetType().FullName
                    });
                });

            stopwatch.Stop();

            _testOutputHelper.WriteLine(
                $"{v} - time : {stopwatch.ElapsedMilliseconds} lock: {isLock}, ticks: {DateTime.UtcNow.Ticks}");

            lockResult.TryAdd(v, isLock);

            return isLock;
        }

        var tasks = new List<Task>();

        // 创建多个并发任务，模拟多个客户端同时争抢锁
        for (int i = 0; i < 10; i++)
        {
            var localIndex = i;
            tasks.Add(TrySetAsyncLock(localIndex.ToString()));
        }

        // 等待所有任务完成
        await Task.WhenAll(tasks);

        await _redisClient.DeleteAsyncLock("*", prefix);

        Assert.True(lockResult.Count == 10);

        var hasLockRequests = lockResult.Count(x => x.Value);

        _testOutputHelper.WriteLine(
            $"hasLockRequests: {hasLockRequests} | delayMs: {delayMs}");

        // 给予上下浮动1的范围
        Assert.True(hasLockRequests == expect || hasLockRequests == expect - 1 || hasLockRequests == expect + 1);
    }

    /// <summary>
    /// 测试 Redis 缓存删除方法在不同延迟和线性请求超时情况下的分布式锁获取情况。
    /// 该测试模拟了多个客户端争抢锁的情况，每个客户端请求删除缓存之前有不同的延迟时间，
    /// 以测试锁机制的有效性，并且在延迟和锁超时的情况下，有多少个请求可以成功获得锁。
    /// </summary>
    /// <param name="prefix"></param>
    /// <param name="key"></param>
    /// <param name="deleteKey"></param>
    /// <param name="value"></param>
    /// <param name="result"></param>
    /// <param name="delayMs"></param>
    /// <param name="hasLockCount"></param>
    [Theory]
    [InlineData("Joe", "1111Joe", "*", "18", null, 100, 5)]
    [InlineData("Joe", "1111Joe", "*", "18", null, 200, 4)]
    [InlineData("Joe", "1111Joe", "*", "18", null, 300, 3)]
    [InlineData("Joe", "1111Joe", "*", "18", null, 700, 2)]
    [InlineData("Joe", "1111Joe", "*", "18", null, 900, 1)]
    public async void TestLockRedisCacheDeleteWithVaryingDelaysAndLinearTimeoutHandling(string prefix, string key,
        string deleteKey, string value, string? result, int delayMs, int hasLockCount)
    {
        var fullKey = $"{prefix}:{key}";

        var client = _redisClient.GetRedisClient()!;

        await _redisClient.SetAsyncLock(fullKey, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        });

        var lockResult = new ConcurrentDictionary<string, bool>();

        // 定义一个异步操作，用于模拟不同客户端争抢锁
        async Task<bool> TrySetAsyncLock(string v)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var isLock = await _redisClient.ExecuteWithRedisLockAsync(client,
                $"{Prefix.DeletePrefix}:{fullKey}", async () =>
                {
                    await Task.Delay(delayMs);
                    await _redisClient.Delete(deleteKey, prefix);
                });

            stopwatch.Stop();

            _testOutputHelper.WriteLine(
                $"{v} - time : {stopwatch.ElapsedMilliseconds} lock: {isLock}, ticks: {DateTime.UtcNow.Ticks}");

            lockResult.TryAdd(v, isLock);

            return isLock;
        }

        var tasks = new List<Task>();

        // 创建多个并发任务，模拟多个客户端同时争抢锁
        for (int i = 0; i < 10; i++)
        {
            var localIndex = i;
            tasks.Add(TrySetAsyncLock(localIndex.ToString()));
        }

        // 等待所有任务完成
        await Task.WhenAll(tasks);

        await _redisClient.DeleteAsyncLock("*", prefix);

        Assert.True(lockResult.Count == 10);

        var hasLockRequests = lockResult.Count(x => x.Value);

        _testOutputHelper.WriteLine(
            $"hasLockRequests: {hasLockRequests} | delayMs: {delayMs}");

        // 给予上下浮动1的范围
        Assert.True(hasLockRequests == hasLockCount || hasLockRequests == hasLockCount - 1 ||
                    hasLockRequests == hasLockCount + 1);

        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }


    [Fact]
    public void TestCanGetRedisClient()
    {
        var redisClient = _redisClient.GetRedisClient();
        Assert.NotNull(redisClient);
    }
}