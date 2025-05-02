using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FastCache.Core.Entity;
using FastCache.Core.Enums;
using FastCache.Redis.Driver;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests;

public partial class RedisCacheTests(ITestOutputHelper testOutputHelper)
{
    private readonly ITestOutputHelper _testOutputHelper = testOutputHelper;

    private readonly RedisCache _redisClient =
        new("localhost:6379,syncTimeout=5000,connectTimeout=5000,responseTimeout=5000", true);

    private class Setting
    {
        public string Name { get; set; }
    }

    private class User
    {
        public string Name { get; set; }

        public List<Setting> Settings { get; set; }
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
        }, TimeSpan.FromSeconds(20));

        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Fact]
    public async Task GetAfterSettingComplexObjectShouldRetrieveOriginalValues()
    {
        var key = "TestRedisCacheCanGet";

        var value = new User()
        {
            Name = "23131",
            Settings =
            [
                new Setting
                {
                    Name = "fas231"
                }
            ]
        };

        await _redisClient.Set(key, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        }, TimeSpan.FromSeconds(20));

        var s = await _redisClient.Get(key);

        Assert.Equal(((User)s.Value).Name, value.Name);
        Assert.Single(((User)s.Value).Settings);
        Assert.Equal("fas231", ((User)s.Value).Settings.First().Name);
    }

    [Fact]
    public async Task TestFuzzySearchAsync()
    {
        var key = "TestFuzzySearchAsync";
        var value = "123456";

        await _redisClient.Set(key, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        }, TimeSpan.FromMinutes(20));

        var result = await _redisClient.FuzzySearchAsync(new AdvancedSearchModel()
        {
            PageSize = 1000,
            Pattern = "Test*"
        });

        Assert.True(result.Count > 0);
        Assert.Contains(key, result);

        await _redisClient.Delete(key);
    }

    [Theory]
    // 基础匹配场景
    [InlineData(100, "Order_", "Order_1*", 10)] // 前缀+数字通配
    [InlineData(500, "Product_", "Product_*123", 20)] // 后缀固定值匹配
    [InlineData(200, "User_", "User_[0-9]??", 15)] // 字符集+占位符组合

    // 新增关键测试场景（含特殊字符和边界情况）
    [InlineData(50, "Temp\\*Key_", "Temp\\*Key_*", 5)] // 转义字符测试
    [InlineData(300, "IDX_", "IDX_[A-F][0-9]", 30)] // 双字符集组合
    [InlineData(150, "Log_", "Log_[1-9][a-z]", 25)] // 数字范围+字母范围
    [InlineData(0, "", "*", 3)] // 空前缀全通配
    [InlineData(80, "Test?", "Test?_[X-Z]", 8)] // key本身含问号
    [InlineData(120, "[Demo]_", "[Demo]_*#*", 12)] // key含方括号

    // Redis实际业务典型场景
    [InlineData(1000, "Session:", "Session:*:Token", 50)] // Redis常见会话模式  
    [InlineData(600, "{Cache}:Item:", "{Cache}:Item:*:Ver", 60)] // Hash tag模式
    [InlineData(400, "@Event@_", "@Event@_*-Log", 40)] // 特殊符号包裹

    // Unicode和多语言支持
    [InlineData(200, "用户_", "用户_[张三李四]订单*", 20)] // UTF-8字符集  
    [InlineData(70, "商品-", "商品-[가-힣]*번호", 7)] // Hangul字母范围
    public async Task FuzzySearchWithParametersAsync(
        int totalRecords,
        string keyPrefix,
        string searchPattern,
        int expectedMatches)
    {
        // Arrange - 准备带明确匹配规则的测试数据
        var insertedKeys = new List<string>();
        var random = new Random();

        var patternGeneratorUtil = new RedisKeyPatternGeneratorUtil();

        // 生成可预测的匹配key（前N条为符合搜索条件的key）
        for (var i = 0; i < totalRecords; i++)
        {
            var shouldMatch = i < expectedMatches;

            // 构造可匹配的key（根据searchPattern反向生成）
            var key = shouldMatch
                ? patternGeneratorUtil.GenerateMatchingKey(searchPattern, i)
                : $"{keyPrefix}:not_match:{Guid.NewGuid()}"; // 不匹配的随机key

            var value = $"Value_{random.Next(1000)}";

            await _redisClient.Set(key, new CacheItem()
            {
                Value = value,
                AssemblyName = value.GetType().Assembly.GetName().Name,
                Type = value.GetType().FullName
            }, TimeSpan.FromSeconds(20));

            insertedKeys.Add(key);
        }

        try
        {
            // Act - 执行模糊查询
            var matchedKeys = await _redisClient.FuzzySearchAsync(new AdvancedSearchModel()
            {
                PageSize = totalRecords * 2, // 确保能返回所有可能结果
                Pattern = searchPattern
            });

            // Assert - 验证结果
            Assert.NotNull(matchedKeys);

            // 验证返回数量是否符合预期
            Assert.Equal(expectedMatches, matchedKeys.Count);

            foreach (var key in matchedKeys)
            {
                // 验证1：Key必须符合Redis通配符规则
                Assert.True(MatchesRedisPattern(key, searchPattern),
                    $"Key '{key}' match pattern '{searchPattern}'");

                // 验证2：Key必须在我们插入的key集合中
                Assert.Contains(key, insertedKeys);
            }
        }
        catch (Exception _)
        {
            await _redisClient.BatchDeleteKeysWithPipelineAsync(insertedKeys);
        }

        // 辅助方法：Redis通配符匹配逻辑
        bool MatchesRedisPattern(string key, string pattern)
        {
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".")
                .Replace(@"\[", "[")
                .Replace(@"\]", "]") + "$";
            return Regex.IsMatch(key, regexPattern);
        }

        var deletedCount = await _redisClient.BatchDeleteKeysWithPipelineAsync(insertedKeys);
        Assert.True(deletedCount == insertedKeys.Count);
    }

    [Theory]
    [InlineData("key1", "18", null, 1)]
    [InlineData("key2", "19", null, 1)]
    public async void TestRedisCacheCanSetTimeout(string key, string value, string? result, long expire = 0)
    {
        await _redisClient.Set(key, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        }, TimeSpan.FromSeconds(expire));

        await Task.Delay(TimeSpan.FromSeconds(3));

        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("anson", "18", null)]
    [InlineData("anson1", "19", null)]
    public async void TestRedisCacheCanDelete(string key, string value, string? result)
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
    public async void TestRedisCacheCanDeleteByLastPattern(string prefix, string key, string value, string? result)
    {
        await _redisClient.Set(key, new CacheItem()
        {
            Value = value,
            AssemblyName = value.GetType().Assembly.GetName().Name,
            Type = value.GetType().FullName
        }, TimeSpan.FromSeconds(10));
        await _redisClient.Delete("anson*", prefix);
        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("", "anson1111", "18", null)]
    [InlineData("", "anson2222", "19", null)]
    public async void TestRedisCacheCanDeleteByLastPatternByFullKey(string prefix, string key, string value,
        string? result)
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
        string? result)
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
        string? result)
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
    public async void TestRedisCacheCanDeleteByFirstPattern(string prefix, string key, string value, string? result)
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
        string? result)
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
        string value, string? result)
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
        string deleteKey, string value, string? result)
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
        string value, string? result)
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
        string deleteKey, string value, string? result)
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
        string deleteKey, string value, string? result)
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

    /// <summary>
    /// 测试 Redis 缓存添加方法在不同延迟和线性请求超时情况下的分布式锁获取情况。
    /// 该测试模拟了多个客户端争抢锁的情况，每个客户端请求之前有不同的延迟时间，
    /// 以测试锁机制的有效性，以及在延迟和锁超时的情况下有多少个请求可以成功获得锁。
    /// </summary>
    /// <param name="prefix"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="retryTime"></param>
    /// <param name="delayMs"></param>
    /// <param name="expect"></param>
    /// <param name="msTimeout"></param>
    [Theory]
    [InlineData("Joe", "1111Joe", "1", 100, 1000, 0,
        10)]
    [InlineData("Joe", "1111Joe", "1", 100, 1000, 100,
        5)]
    [InlineData("Joe", "1111Joe", "1", 100, 1000, 200,
        4)]
    [InlineData("Joe", "1111Joe", "1", 100, 1000, 300,
        3)]
    [InlineData("Joe", "1111Joe", "1", 100, 1000, 700,
        2)]
    [InlineData("Joe", "1111Joe", "1", 100, 1000, 900,
        1)]
    public async void TestLockRedisCacheAddWithVaryingDelaysAndLinearTimeoutHandling(string prefix, string key,
        string value,
        int msTimeout, int retryTime,
        int delayMs, int expect)
    {
        var fullKey = $"{prefix}:{key}";

        var lockWaitTime = TimeSpan.FromMilliseconds(msTimeout);
        var lockRetryTime = TimeSpan.FromMilliseconds(retryTime);

        var lockResult = new ConcurrentDictionary<string, bool>();

        // 定义一个异步操作，用于模拟不同客户端争抢锁
        async Task<DistributedLockResult> TrySetAsyncLock(string v)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var distributedLockResult = await _redisClient.ExecuteWithRedisLockAsync(
                $"lock:delete:{fullKey}", async () =>
                {
                    await Task.Delay(delayMs);
                    await _redisClient.Set(fullKey, new CacheItem()
                    {
                        Value = $"{value}-{v}",
                        AssemblyName = value.GetType().Assembly.GetName().Name,
                        Type = value.GetType().FullName
                    });
                }, new DistributedLockOptions()
                {
                    WaitTime = lockWaitTime,
                    RetryInterval = lockRetryTime
                }, cancellationToken: CancellationToken.None);

            stopwatch.Stop();

            testOutputHelper.WriteLine(
                $"{v} - time : {stopwatch.ElapsedMilliseconds} lock: {distributedLockResult.IsSuccess}, ticks: {DateTime.UtcNow.Ticks}");

            lockResult.TryAdd(v, distributedLockResult.Status == LockStatus.LockNotAcquired);

            return distributedLockResult;
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

        await _redisClient.Delete("*", prefix);

        Assert.Equal(10, lockResult.Count);

        var hasLockRequests = lockResult.Count(x => x.Value);

        testOutputHelper.WriteLine(
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
    /// <param name="msExpire"></param>
    /// <param name="delayMs"></param>
    /// <param name="hasLockCount"></param>
    /// <param name="msTimeout"></param>
    // [Theory]
    // [InlineData("Joe", "1111Joe", "*", "18", null, 100, 1000, 0, 10)]
    // [InlineData("Joe", "1111Joe", "*", "18", null, 100, 1000, 100, 5)]
    // [InlineData("Joe", "1111Joe", "*", "18", null, 100, 1000, 200, 4)]
    // [InlineData("Joe", "1111Joe", "*", "18", null, 100, 1000, 300, 3)]
    // [InlineData("Joe", "1111Joe", "*", "18", null, 100, 1000, 700, 2)]
    // [InlineData("Joe", "1111Joe", "*", "18", null, 100, 1000, 900, 1)]
    // public async void TestLockRedisCacheDeleteWithVaryingDelaysAndLinearTimeoutHandling(string prefix, string key,
    //     string deleteKey, string value, string? result, int msTimeout, int msExpire, int delayMs, int hasLockCount)
    // {
    //     var fullKey = $"{prefix}:{key}";
    //
    //     await _redisClient.Set(fullKey, new CacheItem()
    //     {
    //         Value = value,
    //         AssemblyName = value.GetType().Assembly.GetName().Name,
    //         Type = value.GetType().FullName
    //     });
    //
    //     var lockResult = new ConcurrentDictionary<string, bool>();
    //
    //     // 定义一个异步操作，用于模拟不同客户端争抢锁
    //     async Task<bool> TrySetAsyncLock(string v)
    //     {
    //         var stopwatch = new Stopwatch();
    //         stopwatch.Start();
    //         var isLock = await _redisClient.ExecuteWithRedisLockAsync(
    //             $"lock:delete:{fullKey}", async () =>
    //             {
    //                 await Task.Delay(delayMs);
    //                 await _redisClient.Delete(deleteKey, prefix);
    //             }, msTimeout, msExpire);
    //
    //         stopwatch.Stop();
    //
    //         testOutputHelper.WriteLine(
    //             $"{v} - time : {stopwatch.ElapsedMilliseconds} lock: {isLock}, ticks: {DateTime.UtcNow.Ticks}");
    //
    //         lockResult.TryAdd(v, isLock);
    //
    //         return isLock;
    //     }
    //
    //     var tasks = new List<Task>();
    //
    //     // 创建多个并发任务，模拟多个客户端同时争抢锁
    //     for (int i = 0; i < 10; i++)
    //     {
    //         var localIndex = i;
    //         tasks.Add(TrySetAsyncLock(localIndex.ToString()));
    //     }
    //
    //     // 等待所有任务完成
    //     await Task.WhenAll(tasks);
    //
    //     await _redisClient.Delete("*", prefix);
    //
    //     Assert.True(lockResult.Count == 10);
    //
    //     var hasLockRequests = lockResult.Count(x => x.Value);
    //
    //     testOutputHelper.WriteLine(
    //         $"hasLockRequests: {hasLockRequests} | delayMs: {delayMs}");
    //
    //     // 给予上下浮动1的范围
    //     Assert.True(hasLockRequests == hasLockCount || hasLockRequests == hasLockCount - 1 ||
    //                 hasLockRequests == hasLockCount + 1);
    //
    //     var s = await _redisClient.Get(key);
    //     Assert.Equal(s.Value, result);
    // }

    /// <summary>
    /// 测试 Redis 缓存添加方法在不同延迟和线性请求超时情况下的分布式锁自动释放以及抢锁情况。
    /// </summary>
    /// <param name="prefix"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="msTimeout"></param>
    /// <param name="msExpire"></param>
    /// <param name="delayMs"></param>
    /// <param name="expect"></param>
    // [Theory]
    // [InlineData("Joe", "1111Joe", "88888888888888888888888888888888888888888888888888888888888888888", 100, 200, 200,
    //     1)]
    // [InlineData("Joe", "1111Joe", "88888888888888888888888888888888888888888888888888888888888888888", 400, 200, 200,
    //     2)]
    // public async void TestLockAutoEvictByAdd(string prefix, string key,
    //     string value,
    //     int msTimeout, int msExpire,
    //     int delayMs, int expect)
    // {
    //     var fullKey = $"{prefix}:{key}";
    //
    //     var lockResult = new ConcurrentDictionary<string, bool>();
    //
    //     // 定义一个异步操作，用于模拟不同客户端争抢锁
    //     async Task<bool> TrySetAsyncLock(string v)
    //     {
    //         var stopwatch = new Stopwatch();
    //         stopwatch.Start();
    //         var isLock = await _redisClient.ExecuteWithRedisLockAsync(
    //             $"lock:delete:{fullKey}", async () =>
    //             {
    //                 await Task.Delay(delayMs);
    //                 await _redisClient.Set(fullKey, new CacheItem()
    //                 {
    //                     Value = $"{value}-{v}",
    //                     AssemblyName = value.GetType().Assembly.GetName().Name,
    //                     Type = value.GetType().FullName
    //                 });
    //             }, msTimeout, msExpire);
    //
    //         stopwatch.Stop();
    //
    //         testOutputHelper.WriteLine(
    //             $"{v} - time : {stopwatch.ElapsedMilliseconds} lock: {isLock}, ticks: {DateTime.UtcNow.Ticks}");
    //
    //         lockResult.TryAdd(v, isLock);
    //
    //         return isLock;
    //     }
    //
    //     var tasks = new List<Task>();
    //
    //     // 创建多个并发任务，模拟多个客户端同时争抢锁
    //     for (int i = 0; i < 2; i++)
    //     {
    //         var localIndex = i;
    //         tasks.Add(TrySetAsyncLock(localIndex.ToString()));
    //     }
    //
    //     // 等待所有任务完成
    //     await Task.WhenAll(tasks);
    //
    //     await _redisClient.Delete("*", prefix);
    //
    //     var hasLockRequests = lockResult.Count(x => x.Value);
    //
    //     testOutputHelper.WriteLine(
    //         $"hasLockRequests: {hasLockRequests} | delayMs: {delayMs}");
    //
    //     Assert.True(hasLockRequests == expect);
    // }

    /// <summary>
    /// 测试 Redis 缓存删除方法在不同延迟和线性请求超时情况下的分布式锁自动释放以及抢锁情况。
    /// </summary>
    /// <param name="prefix"></param>
    /// <param name="key"></param>
    /// <param name="deleteKey"></param>
    /// <param name="value"></param>
    /// <param name="result"></param>
    /// <param name="msTimeout"></param>
    /// <param name="msExpire"></param>
    /// <param name="delayMs"></param>
    /// <param name="hasLockCount"></param>
    // [Theory]
    // // 抢锁失败的情况
    // [InlineData("Joe", "1111Joe", "*", "18", null, 100, 200, 200, 1)]
    // // 锁超时被抢
    // [InlineData("Joe", "1111Joe", "*", "18", null, 400, 200, 200, 2)]
    // public async void TestLockAutoEvictByDelete(string prefix, string key,
    //     string deleteKey, string value, string? result, int msTimeout, int msExpire, int delayMs, int hasLockCount)
    // {
    //     var fullKey = $"{prefix}:{key}";
    //
    //     await _redisClient.Set(fullKey, new CacheItem()
    //     {
    //         Value = value,
    //         AssemblyName = value.GetType().Assembly.GetName().Name,
    //         Type = value.GetType().FullName
    //     });
    //
    //     var lockResult = new ConcurrentDictionary<string, bool>();
    //
    //     // 定义一个异步操作，用于模拟不同客户端争抢锁
    //     async Task<bool> TrySetAsyncLock(string v)
    //     {
    //         var stopwatch = new Stopwatch();
    //         stopwatch.Start();
    //         var isLock = await _redisClient.ExecuteWithRedisLockAsync(
    //             $"lock:delete:{fullKey}", async () =>
    //             {
    //                 await Task.Delay(delayMs);
    //                 await _redisClient.Delete(deleteKey, prefix);
    //             }, msTimeout, msExpire);
    //
    //         stopwatch.Stop();
    //
    //         testOutputHelper.WriteLine(
    //             $"{v} - time : {stopwatch.ElapsedMilliseconds} lock: {isLock}, ticks: {DateTime.UtcNow.Ticks}");
    //
    //         lockResult.TryAdd(v, isLock);
    //
    //         return isLock;
    //     }
    //
    //     var tasks = new List<Task>();
    //
    //     // 创建多个并发任务，模拟多个客户端同时争抢锁
    //     for (int i = 0; i < 2; i++)
    //     {
    //         var localIndex = i;
    //         tasks.Add(TrySetAsyncLock(localIndex.ToString()));
    //     }
    //
    //     // 等待所有任务完成
    //     await Task.WhenAll(tasks);
    //
    //     await _redisClient.Delete("*", prefix);
    //
    //     var hasLockRequests = lockResult.Count(x => x.Value);
    //
    //     testOutputHelper.WriteLine(
    //         $"hasLockRequests: {hasLockRequests} | delayMs: {delayMs}");
    //
    //     Assert.True(hasLockRequests == hasLockCount);
    //
    //     var s = await _redisClient.Get(key);
    //     Assert.Equal(s.Value, result);
    // }

    // [Theory]
    // [InlineData("Joe", "1111Joe", "*", "18", null, 100, 1000, 100, 1000, 0.5)] // 模拟1000个请求并发
    // [InlineData("Joe", "1111Joe", "*", "18", null, 100, 1000, 100, 2000, 0.5)] // 模拟2000个请求并发
    // [InlineData("Joe", "1111Joe", "*", "18", null, 100, 1000, 700, 1000, 0.1)] // 模拟1000个请求并发
    // [InlineData("Joe", "1111Joe", "*", "18", null, 100, 1000, 300, 1000, 0.3)] // 模拟1000个请求并发
    // public async void PerformanceTestLockRedisCacheUnderHighConcurrency(
    //     string prefix, string key, string deleteKey, string value, string? result,
    //     int msTimeout, int msExpire, int delayMs, int numberOfRequests, decimal expectedSuccessRate)
    // {
    //     var fullKey = $"{prefix}:{key}";
    //
    //     // 初始化缓存数据
    //     await _redisClient.Set(fullKey, new CacheItem()
    //     {
    //         Value = value,
    //         AssemblyName = value.GetType().Assembly.GetName().Name,
    //         Type = value.GetType().FullName
    //     });
    //
    //     var lockResult = new ConcurrentDictionary<string, bool>();
    //     var responseTimes = new ConcurrentBag<long>(); // 存储每个请求的响应时间
    //
    //     // 定义一个异步操作，用于模拟不同客户端争抢锁
    //     async Task<bool> TrySetAsyncLock(string v)
    //     {
    //         var stopwatch = new Stopwatch();
    //         stopwatch.Start();
    //
    //         var isLock = await _redisClient.ExecuteWithRedisLockAsync(
    //             $"lock:delete:{fullKey}", async () =>
    //             {
    //                 await Task.Delay(delayMs); // 模拟主体操作前的延迟
    //                 await _redisClient.Delete(deleteKey, prefix);
    //             }, msTimeout, msExpire);
    //
    //         stopwatch.Stop();
    //
    //         // _testOutputHelper.WriteLine(
    //         //     $"{v} - time : {stopwatch.ElapsedMilliseconds}ms lock: {isLock}, ticks: {DateTime.UtcNow.Ticks}");
    //
    //         lockResult.TryAdd(v, isLock);
    //         responseTimes.Add(stopwatch.ElapsedMilliseconds); // 记录响应时间
    //
    //         return isLock;
    //     }
    //
    //     var tasks = new List<Task>();
    //
    //     // 创建高并发任务，模拟多个客户端同时争抢锁
    //     for (int i = 0; i < numberOfRequests; i++)
    //     {
    //         var localIndex = i;
    //         tasks.Add(TrySetAsyncLock(localIndex.ToString()));
    //     }
    //
    //     // 等待所有任务完成
    //     await Task.WhenAll(tasks);
    //
    //     // 删除缓存
    //     await _redisClient.Delete("*", prefix);
    //
    //     Assert.True(lockResult.Count == numberOfRequests);
    //
    //     var hasLockRequests = lockResult.Count(x => x.Value);
    //
    //     testOutputHelper.WriteLine($"hasLockRequests: {hasLockRequests} | delayMs: {delayMs}");
    //
    //     // 验证最后缓存项的值
    //     var s = await _redisClient.Get(key);
    //     Assert.Equal(s.Value, result);
    //
    //     // 性能分析: 计算平均响应时间
    //     var averageResponseTime = responseTimes.Average();
    //     var maxResponseTime = responseTimes.Max();
    //     var minResponseTime = responseTimes.Min();
    //
    //     var expectedSuccessCount = (int)(numberOfRequests * expectedSuccessRate);
    //
    //     // 打印性能测试结果
    //     testOutputHelper.WriteLine($"Performance Test Results:");
    //     testOutputHelper.WriteLine($"Total Requests: {numberOfRequests}");
    //     testOutputHelper.WriteLine($"Locks Acquired: {hasLockRequests}");
    //     testOutputHelper.WriteLine($"Average Response Time: {averageResponseTime}ms");
    //     testOutputHelper.WriteLine($"Max Response Time: {maxResponseTime}ms");
    //     testOutputHelper.WriteLine($"Min Response Time: {minResponseTime}ms");
    //     testOutputHelper.WriteLine($"Expected Success Count: {expectedSuccessCount} | rate: {expectedSuccessRate}");
    //
    //     // 对于高并发，确保大部分锁能在规定时间内被获取
    //     Assert.True(hasLockRequests >= expectedSuccessCount);
    // }

    /// <summary>
    /// 测试 Redis 缓存添加方法在不同延迟和线性请求超时情况下的分布式锁获取情况。
    /// 该测试模拟了多个线程争抢锁的情况，每个线程请求之前有不同的延迟时间，
    /// 以测试锁机制的有效性，以及在延迟和锁超时的情况下有多少个请求可以成功获得锁。
    /// </summary>
    /// <param name="prefix"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="msTimeout"></param>
    /// <param name="msExpire"></param>
    /// <param name="delayMs"></param>
    /// <param name="numberOfRequests"></param>
    /// <param name="expectedSuccessRate"></param>
    // [Theory]
    // [InlineData("Joe", "1111Joe", "88888888888888888888888888888888888888888888888888888888888888888", 500, 1000, 200,
    //     10,
    //     0.1)]
    // [InlineData("Joe", "1111Joe", "88888888888888888888888888888888888888888888888888888888888888888", 500, 1000, 0, 10,
    //     0.4)]
    // [InlineData("Joe", "1111Joe", "88888888888888888888888888888888888888888888888888888888888888888", 300, 1000, 0, 10,
    //     0.1)]
    // [InlineData("Joe", "1111Joe", "88888888888888888888888888888888888888888888888888888888888888888", 100, 1000, 0,
    //     10, 0.1)]
    // [InlineData("Joe", "1111Joe", "88888888888888888888888888888888888888888888888888888888888888888", 100, 1000, 100,
    //     10, 0.1)]
    // [InlineData("Joe", "JoeKey", "SampleValue", 100, 1000, 200, 10, 0.1)]
    // public async void TestLockRedisCacheAddWithMultithreadingAndLockValidation(string prefix, string key, string value,
    //     int msTimeout, int msExpire, int delayMs, int numberOfRequests, decimal expectedSuccessRate)
    // {
    //     var fullKey = $"{prefix}:{key}";
    //     var lockResult = new ConcurrentDictionary<string, bool>();
    //
    //     // 初始化缓存数据
    //     await _redisClient.Set(fullKey, new CacheItem()
    //     {
    //         Value = value,
    //         AssemblyName = value.GetType().Assembly.GetName().Name,
    //         Type = value.GetType().FullName
    //     });
    //
    //     // 创建一个 ManualResetEventSlim 用于控制并发的起始时间
    //     var startEvent = new ManualResetEventSlim(false);
    //
    //     // 线程安全的响应时间容器
    //     var responseTimes = new ConcurrentBag<long>();
    //
    //     // 定义一个异步操作，用于模拟不同线程争抢锁
    //     async Task TrySetAsyncLock(string threadId)
    //     {
    //         try
    //         {
    //             // 等待所有线程就绪
    //             startEvent.Wait();
    //
    //             var stopwatch = Stopwatch.StartNew();
    //
    //             // 执行带锁操作
    //             var isLock = await _redisClient.ExecuteWithRedisLockAsync(
    //                 $"lock:delete:{fullKey}", async () =>
    //                 {
    //                     await Task.Delay(delayMs); // 模拟处理延迟
    //
    //                     // 假设此处可能发生异常，需记录日志
    //                     await _redisClient.Set(fullKey, new CacheItem()
    //                     {
    //                         Value = $"{value}-{threadId}",
    //                         AssemblyName = value.GetType().Assembly.GetName().Name,
    //                         Type = value.GetType().FullName
    //                     });
    //                 }, msTimeout, msExpire);
    //
    //             stopwatch.Stop();
    //
    //             testOutputHelper.WriteLine(
    //                 $"threadId: {threadId} | responseTimes: {stopwatch.ElapsedMilliseconds}ms | isLock: {isLock} | startWatch: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
    //
    //             responseTimes.Add(stopwatch.ElapsedMilliseconds);
    //
    //             // 添加锁结果
    //             lockResult.TryAdd(threadId, isLock);
    //         }
    //         catch (Exception ex)
    //         {
    //             // 捕获并记录所有可能的异常
    //             testOutputHelper.WriteLine($"Error in threadId: {threadId} - {ex.Message}");
    //         }
    //     }
    //
    //     var tasks = new List<Task>();
    //     var lockObject = new object();
    //
    //     // 使用 Parallel.For 来模拟多线程并发
    //     Parallel.For(0, 10, i =>
    //     {
    //         var index = i;
    //         var task = Task.Run(async () => { await TrySetAsyncLock(index.ToString()); });
    //
    //         lock (lockObject)
    //         {
    //             tasks.Add(task);
    //         }
    //     });
    //
    //     // 手动触发所有线程开始执行
    //     startEvent.Set();
    //
    //     // 等待所有线程完成任务
    //     await Task.WhenAll(tasks);
    //
    //     Assert.True(!tasks.Any(x => x == null));
    //
    //     Assert.True(tasks.Count > 0);
    //
    //     // 删除缓存
    //     await _redisClient.Delete("*", prefix);
    //
    //     Assert.True(lockResult.Count == numberOfRequests);
    //
    //     var expectedSuccessCount = (int)(numberOfRequests * expectedSuccessRate);
    //
    //     // 计算获取到锁的请求数
    //     var hasLockRequests = lockResult.Count(x => x.Value);
    //
    //     testOutputHelper.WriteLine($"hasLockRequests: {hasLockRequests} | delayMs: {delayMs}");
    //
    //     // 给予上下浮动1的范围，验证实际锁获取数是否符合预期
    //     Assert.True(hasLockRequests >= expectedSuccessCount);
    //
    //     // 打印性能测试结果
    //     var averageResponseTime = responseTimes.Average();
    //     testOutputHelper.WriteLine($"Average Response Time: {averageResponseTime}ms");
    // }


    /// <summary>
    /// 测试 Redis 缓存添加方法在不同延迟和线性请求超时情况下的分布式锁获取情况。
    /// 该测试模拟了多个线程争抢锁的情况，每个线程请求之前有不同的延迟时间，
    /// 以测试锁机制的有效性，以及在延迟和锁超时的情况下有多少个请求可以成功获得锁。
    /// </summary>
    /// <param name="prefix"></param>
    /// <param name="key"></param>
    /// <param name="deleteKey"></param>
    /// <param name="value"></param>
    /// <param name="msTimeout"></param>
    /// <param name="msExpire"></param>
    /// <param name="delayMs"></param>
    /// <param name="numberOfRequests"></param>
    /// <param name="expectedSuccessRate"></param>
    // [Theory]
    // [InlineData("Joe", "1111Joe", "*", "88888888888888888888888888888888888888888888888888888888888888888", 500, 1000,
    //     200, 10,
    //     0.1)]
    // [InlineData("Joe", "1111Joe", "*", "88888888888888888888888888888888888888888888888888888888888888888", 500, 1000,
    //     0, 10,
    //     0.3)]
    // [InlineData("Joe", "1111Joe", "*", "88888888888888888888888888888888888888888888888888888888888888888", 300, 1000,
    //     0, 10,
    //     0.2)]
    // [InlineData("Joe", "1111Joe", "*", "88888888888888888888888888888888888888888888888888888888888888888", 100, 1000,
    //     0,
    //     10, 0.1)]
    // [InlineData("Joe", "1111Joe", "*", "88888888888888888888888888888888888888888888888888888888888888888", 100, 1000,
    //     100,
    //     10, 0.1)]
    // [InlineData("Joe", "JoeKey", "*", "SampleValue", 100, 1000, 200, 10, 0.1)]
    // public async void TestLockRedisCacheDeleteWithMultithreadingAndLockValidation(string prefix, string key,
    //     string deleteKey, string value,
    //     int msTimeout, int msExpire, int delayMs, int numberOfRequests, decimal expectedSuccessRate)
    // {
    //     var fullKey = $"{prefix}:{key}";
    //     var lockResult = new ConcurrentDictionary<string, bool>();
    //
    //     // 初始化缓存数据
    //     await _redisClient.Set(fullKey, new CacheItem()
    //     {
    //         Value = value,
    //         AssemblyName = value.GetType().Assembly.GetName().Name,
    //         Type = value.GetType().FullName
    //     });
    //
    //     // 创建一个 ManualResetEventSlim 用于控制并发的起始时间
    //     var startEvent = new ManualResetEventSlim(false);
    //
    //     // 线程安全的响应时间容器
    //     var responseTimes = new ConcurrentBag<long>();
    //
    //     // 定义一个异步操作，用于模拟不同线程争抢锁
    //     async Task TrySetAsyncLock(string threadId)
    //     {
    //         try
    //         {
    //             // 等待所有线程就绪
    //             startEvent.Wait();
    //
    //             var stopwatch = Stopwatch.StartNew();
    //
    //             // 执行带锁操作
    //             var isLock = await _redisClient.ExecuteWithRedisLockAsync(
    //                 $"lock:delete:{fullKey}", async () =>
    //                 {
    //                     await Task.Delay(delayMs); // 模拟处理延迟
    //
    //                     await _redisClient.Delete(deleteKey, prefix);
    //
    //                     // 假设此处可能发生异常，需记录日志
    //                     await _redisClient.Set(fullKey, new CacheItem()
    //                     {
    //                         Value = $"{value}-{threadId}",
    //                         AssemblyName = value.GetType().Assembly.GetName().Name,
    //                         Type = value.GetType().FullName
    //                     });
    //                 }, msTimeout, msExpire);
    //
    //             stopwatch.Stop();
    //
    //             testOutputHelper.WriteLine(
    //                 $"threadId: {threadId} | responseTimes: {stopwatch.ElapsedMilliseconds}ms | isLock: {isLock} | startWatch: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
    //
    //             responseTimes.Add(stopwatch.ElapsedMilliseconds);
    //
    //             // 添加锁结果
    //             lockResult.TryAdd(threadId, isLock);
    //         }
    //         catch (Exception ex)
    //         {
    //             // 捕获并记录所有可能的异常
    //             testOutputHelper.WriteLine($"Error in threadId: {threadId} - {ex.Message}");
    //         }
    //     }
    //
    //     var tasks = new List<Task>();
    //     var lockObject = new object();
    //
    //     // 使用 Parallel.For 来模拟多线程并发
    //     Parallel.For(0, 10, i =>
    //     {
    //         var index = i;
    //         var task = Task.Run(async () => { await TrySetAsyncLock(index.ToString()); });
    //
    //         lock (lockObject)
    //         {
    //             tasks.Add(task);
    //         }
    //     });
    //
    //     // 手动触发所有线程开始执行
    //     startEvent.Set();
    //
    //     Assert.True(tasks.Count > 0);
    //
    //     // 等待所有线程完成任务
    //     await Task.WhenAll(tasks);
    //
    //     // 删除缓存
    //     await _redisClient.Delete("*", prefix);
    //
    //     testOutputHelper.WriteLine($"lockResult count: {lockResult.Count}");
    //
    //     Assert.True(lockResult.Count == numberOfRequests);
    //
    //     var expectedSuccessCount = (int)(numberOfRequests * expectedSuccessRate);
    //
    //     // 计算获取到锁的请求数
    //     var hasLockRequests = lockResult.Count(x => x.Value);
    //
    //     testOutputHelper.WriteLine($"hasLockRequests: {hasLockRequests} | delayMs: {delayMs}");
    //
    //     // 给予上下浮动1的范围，验证实际锁获取数是否符合预期
    //     Assert.True(hasLockRequests >= expectedSuccessCount);
    //
    //     // 打印性能测试结果
    //     var averageResponseTime = responseTimes.Average();
    //     testOutputHelper.WriteLine($"Average Response Time: {averageResponseTime}ms");
    // }

    /// <summary>
    /// 测试 Redis 缓存添加方法在不同延迟和线性请求超时情况下的分布式锁获取情况。
    /// 该测试模拟了多个线程争抢锁的情况，每个线程请求之前有不同的延迟时间，
    /// 以测试锁机制的有效性，以及在延迟和锁超时的情况下有多少个请求可以成功获得锁。
    /// </summary>
    /// <param name="prefix"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="msTimeout"></param>
    /// <param name="msExpire"></param>
    /// <param name="delayMs"></param>
    /// <param name="numberOfThreads"></param>
    /// <param name="requestsPerThread"></param>
    /// <param name="expectedSuccessRate"></param>
    // [Theory]
    // [InlineData("Joe", "1111Joe", "88888888888888888888888888888888888888888888888888888888888888888", 500, 1000, 200,
    //     10, 5, 0.2)]
    // [InlineData("Joe", "1111Joe", "88888888888888888888888888888888888888888888888888888888888888888", 500, 1000, 0, 10,
    //     5, 0.4)]
    // public async void TestLockRedisCacheAddWithMultipleRequestsAndMultithreading(string prefix, string key,
    //     string value,
    //     int msTimeout, int msExpire, int delayMs, int numberOfThreads, int requestsPerThread,
    //     decimal expectedSuccessRate)
    // {
    //     var fullKey = $"{prefix}:{key}";
    //     var lockResult = new ConcurrentDictionary<string, bool>();
    //
    //     // 初始化缓存数据
    //     await _redisClient.Set(fullKey, new CacheItem()
    //     {
    //         Value = value,
    //         AssemblyName = value.GetType().Assembly.GetName().Name,
    //         Type = value.GetType().FullName
    //     });
    //
    //     // 创建一个 ManualResetEventSlim 用于控制并发的起始时间
    //     var startEvent = new ManualResetEventSlim(false);
    //
    //     // 线程安全的响应时间容器
    //     var responseTimes = new ConcurrentBag<long>();
    //
    //     // 定义一个异步操作，用于模拟不同线程争抢锁，每个线程内发起多个请求
    //     async Task TrySetAsyncLock(string threadId)
    //     {
    //         try
    //         {
    //             // 等待所有线程就绪
    //             startEvent.Wait();
    //
    //             for (int i = 0; i < requestsPerThread; i++)
    //             {
    //                 var stopwatch = Stopwatch.StartNew();
    //
    //                 // 执行带锁操作
    //                 var isLock = await _redisClient.ExecuteWithRedisLockAsync(
    //                     $"lock:delete:{fullKey}", async () =>
    //                     {
    //                         await Task.Delay(delayMs); // 模拟处理延迟
    //
    //                         // 假设此处可能发生异常，需记录日志
    //                         await _redisClient.Set(fullKey, new CacheItem()
    //                         {
    //                             Value = $"{value}-{threadId}-{i}",
    //                             AssemblyName = value.GetType().Assembly.GetName().Name,
    //                             Type = value.GetType().FullName
    //                         });
    //                     }, msTimeout, msExpire);
    //
    //                 stopwatch.Stop();
    //
    //                 testOutputHelper.WriteLine(
    //                     $"threadId: {threadId} | request: {i} | responseTimes: {stopwatch.ElapsedMilliseconds}ms | isLock: {isLock} | time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
    //
    //                 responseTimes.Add(stopwatch.ElapsedMilliseconds);
    //
    //                 // 添加锁结果
    //                 lockResult.TryAdd($"{threadId}-{i}", isLock);
    //             }
    //         }
    //         catch (Exception ex)
    //         {
    //             // 捕获并记录所有可能的异常
    //             testOutputHelper.WriteLine($"Error in threadId: {threadId} - {ex.Message}");
    //         }
    //     }
    //
    //     var tasks = new List<Task>();
    //     var lockObject = new object();
    //
    //     // 使用 Parallel.For 来模拟多线程并发
    //     Parallel.For(0, numberOfThreads, i =>
    //     {
    //         var index = i;
    //         var task = Task.Run(async () => { await TrySetAsyncLock(index.ToString()); });
    //
    //         lock (lockObject)
    //         {
    //             tasks.Add(task);
    //         }
    //     });
    //
    //     // 手动触发所有线程开始执行
    //     startEvent.Set();
    //
    //     // 等待所有线程完成任务
    //     await Task.WhenAll(tasks);
    //
    //     // 删除缓存
    //     await _redisClient.Delete("*", prefix);
    //
    //     testOutputHelper.WriteLine($"lockResult count: {lockResult.Count}");
    //
    //     var totalRequests = numberOfThreads * requestsPerThread;
    //     Assert.True(lockResult.Count == totalRequests);
    //
    //     var expectedSuccessCount = (int)(totalRequests * expectedSuccessRate);
    //
    //     // 计算获取到锁的请求数
    //     var hasLockRequests = lockResult.Count(x => x.Value);
    //
    //     testOutputHelper.WriteLine($"hasLockRequests: {hasLockRequests} | delayMs: {delayMs}");
    //
    //     // 给予上下浮动1的范围，验证实际锁获取数是否符合预期
    //     Assert.True(hasLockRequests >= expectedSuccessCount);
    //
    //     // 打印性能测试结果
    //     var averageResponseTime = responseTimes.Average();
    //     testOutputHelper.WriteLine($"Average Response Time: {averageResponseTime}ms");
    // }

    /// <summary>
    /// 测试 Redis 缓存删除方法在不同延迟和线性请求超时情况下的分布式锁获取情况。
    /// 该测试模拟了多个线程争抢锁的情况，每个线程请求之前有不同的延迟时间，
    /// 以测试锁机制的有效性，以及在延迟和锁超时的情况下有多少个请求可以成功获得锁。
    /// </summary>
    /// <param name="prefix"></param>
    /// <param name="key"></param>
    /// <param name="deleteKey"></param>
    /// <param name="value"></param>
    /// <param name="msTimeout"></param>
    /// <param name="msExpire"></param>
    /// <param name="delayMs"></param>
    /// <param name="numberOfThreads"></param>
    /// <param name="requestsPerThread"></param>
    /// <param name="expectedSuccessRate"></param>
    // [Theory]
    // [InlineData("Joe", "1111Joe", "*", "88888888888888888888888888888888888888888888888888888888888888888", 500, 1000,
    //     200,
    //     10, 5, 0.15)]
    // [InlineData("Joe", "1111Joe", "*", "88888888888888888888888888888888888888888888888888888888888888888", 500, 1000,
    //     0, 10,
    //     5, 0.4)]
    // public async void TestLockRedisCacheDeleteWithMultipleRequestsAndMultithreading(string prefix, string key,
    //     string deleteKey,
    //     string value,
    //     int msTimeout, int msExpire, int delayMs, int numberOfThreads, int requestsPerThread,
    //     decimal expectedSuccessRate)
    // {
    //     var fullKey = $"{prefix}:{key}";
    //     var lockResult = new ConcurrentDictionary<string, bool>();
    //
    //     // 初始化缓存数据
    //     await _redisClient.Set(fullKey, new CacheItem()
    //     {
    //         Value = value,
    //         AssemblyName = value.GetType().Assembly.GetName().Name,
    //         Type = value.GetType().FullName
    //     });
    //
    //     // 创建一个 ManualResetEventSlim 用于控制并发的起始时间
    //     var startEvent = new ManualResetEventSlim(false);
    //
    //     // 线程安全的响应时间容器
    //     var responseTimes = new ConcurrentBag<long>();
    //
    //     // 定义一个异步操作，用于模拟不同线程争抢锁，每个线程内发起多个请求
    //     async Task TrySetAsyncLock(string threadId)
    //     {
    //         try
    //         {
    //             // 等待所有线程就绪
    //             startEvent.Wait();
    //
    //             for (int i = 0; i < requestsPerThread; i++)
    //             {
    //                 var stopwatch = Stopwatch.StartNew();
    //
    //                 // 执行带锁操作
    //                 var isLock = await _redisClient.ExecuteWithRedisLockAsync(
    //                     $"lock:delete:{fullKey}", async () =>
    //                     {
    //                         await Task.Delay(delayMs); // 模拟处理延迟
    //
    //                         await _redisClient.Delete(deleteKey, prefix);
    //
    //                         // 假设此处可能发生异常，需记录日志
    //                         await _redisClient.Set(fullKey, new CacheItem()
    //                         {
    //                             Value = $"{value}-{threadId}-{i}",
    //                             AssemblyName = value.GetType().Assembly.GetName().Name,
    //                             Type = value.GetType().FullName
    //                         });
    //                     }, msTimeout, msExpire);
    //
    //                 stopwatch.Stop();
    //
    //                 testOutputHelper.WriteLine(
    //                     $"threadId: {threadId} | request: {i} | responseTimes: {stopwatch.ElapsedMilliseconds}ms | isLock: {isLock} | time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
    //
    //                 responseTimes.Add(stopwatch.ElapsedMilliseconds);
    //
    //                 // 添加锁结果
    //                 lockResult.TryAdd($"{threadId}-{i}", isLock);
    //             }
    //         }
    //         catch (Exception ex)
    //         {
    //             // 捕获并记录所有可能的异常
    //             testOutputHelper.WriteLine($"Error in threadId: {threadId} - {ex.Message}");
    //         }
    //     }
    //
    //     var tasks = new List<Task>();
    //     var lockObject = new object();
    //
    //     // 使用 Parallel.For 来模拟多线程并发
    //     Parallel.For(0, numberOfThreads, i =>
    //     {
    //         var index = i;
    //         var task = Task.Run(async () => { await TrySetAsyncLock(index.ToString()); });
    //
    //         lock (lockObject)
    //         {
    //             tasks.Add(task);
    //         }
    //     });
    //
    //     // 手动触发所有线程开始执行
    //     startEvent.Set();
    //
    //     // 等待所有线程完成任务
    //     await Task.WhenAll(tasks);
    //
    //     // 删除缓存
    //     await _redisClient.Delete("*", prefix);
    //
    //     var totalRequests = numberOfThreads * requestsPerThread;
    //     Assert.True(lockResult.Count == totalRequests);
    //
    //     var expectedSuccessCount = (int)(totalRequests * expectedSuccessRate);
    //
    //     // 计算获取到锁的请求数
    //     var hasLockRequests = lockResult.Count(x => x.Value);
    //
    //     testOutputHelper.WriteLine($"hasLockRequests: {hasLockRequests} | delayMs: {delayMs}");
    //
    //     // 给予上下浮动1的范围，验证实际锁获取数是否符合预期
    //     Assert.True(hasLockRequests >= expectedSuccessCount);
    //
    //     // 打印性能测试结果
    //     var averageResponseTime = responseTimes.Average();
    //     testOutputHelper.WriteLine($"Average Response Time: {averageResponseTime}ms");
    // }
    [Fact]
    public void TestCanGetRedisClient()
    {
        var redisClient = _redisClient.GetConnectionMultiplexer();
        Assert.NotNull(redisClient);
    }
}