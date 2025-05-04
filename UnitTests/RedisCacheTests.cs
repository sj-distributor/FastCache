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
    private readonly RedisCache _redisClient =
        new("localhost:6379,syncTimeout=5000,connectTimeout=5000,responseTimeout=5000");

    private readonly RedisCache _redisClient2 =
        new("localhost:6379,syncTimeout=5000,connectTimeout=5000,responseTimeout=5000", new RedisCacheOptions()
        {
            QuorumRetryCount = 1,
        });

    private readonly RedisCache _redisClient3 =
        new("localhost:6379,syncTimeout=5000,connectTimeout=5000,responseTimeout=5000", new RedisCacheOptions()
        {
            QuorumRetryCount = 1,
            QuorumRetryDelayMs = 200
        });

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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="totalRecords"></param>
    /// <param name="searchPattern"></param>
    /// <param name="expectedMatches">期待的查找到的值，目前生成的key会有可能重复，在测试中会在进行一查找生成的匹配key的值</param>
    [Theory]
    // 基础匹配场景
    [InlineData(5, "Order_1*", 5)] // 前缀+数字通配
    [InlineData(500, "Product_*123", 5)] // 后缀固定值匹配
    [InlineData(200, "User_[0-9]??", 5)] // 字符集+占位符组合

    // 新增关键测试场景（含特殊字符和边界情况）
    [InlineData(50, "Temp\\*Key_*", 5)] // 转义字符测试
    [InlineData(300, "IDX_[A-F][0-9]", 5)] // 双字符集组合
    [InlineData(150, "Log_[1-9][a-z]", 5)] // 数字范围+字母范围
    [InlineData(5, "*", 5)] // 空前缀全通配
    [InlineData(80, "Test?_[A-Z]", 5)] // key本身含问号
    [InlineData(120, "[Demo]_*#*", 0)] // key含方括号
    [InlineData(120, "[Demo]_*#*", 5)] // key含方括号

    // Redis实际业务典型场景
    [InlineData(1000, "Session:*:Token", 5)] // Redis常见会话模式  
    [InlineData(600, "{Cache}:Item:*:Ver", 5)] // Hash tag模式
    [InlineData(400, "@Event@_*-Log", 5)] // 特殊符号包裹

    // Unicode和多语言支持
    [InlineData(200, "用户_[张三李四]订单*", 0)] // UTF-8字符集  
    [InlineData(70, "商品-[가-힣]*번호", 7)] // Hangul字母范围
    public async Task FuzzySearchWithParametersAsync(
        int totalRecords,
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
                ? patternGeneratorUtil.GenerateRedisCompatibleKey(searchPattern, i)
                : $"{nameof(FuzzySearchWithParametersAsync)}:not_match:{Guid.NewGuid()}"; // 不匹配的随机key

            var value = $"Value_{random.Next(1000)}";

            var setSuccess = await _redisClient.Set(key, new CacheItem()
            {
                Value = value,
                AssemblyName = value.GetType().Assembly.GetName().Name,
                Type = value.GetType().FullName
            }, TimeSpan.FromMinutes(20));

            Assert.True(setSuccess);

            insertedKeys.Add(key);
        }

        var insertedDictKeys = insertedKeys.Distinct().ToList();

        var generateMatchedKeys = insertedDictKeys.Where(x => !x.Contains("not_match")).ToList();

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
            Assert.Equal(generateMatchedKeys.Count, matchedKeys.Count);

            foreach (var key in matchedKeys)
            {
                // 验证2：Key必须在我们插入的key集合中
                Assert.Contains(key, insertedKeys);
            }
        }
        catch (Exception)
        {
            var deletedCount = await _redisClient.BatchDeleteKeysWithPipelineAsync(insertedKeys);
            Assert.True(deletedCount == insertedKeys.Count);
            throw;
        }

        var deletedCountByNormalFlow = await _redisClient.BatchDeleteKeysWithPipelineAsync(insertedKeys);
        Assert.True(deletedCountByNormalFlow == insertedKeys.Count);

        await Task.Delay(TimeSpan.FromSeconds(2));
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
    [InlineData("anson1111", "18", null)]
    [InlineData("anson2222", "19", null)]
    public async void TestRedisCacheCanDeleteByLastPatternByFullKey(string key, string value,
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

    [Fact]
    public void TestCanGetRedisClient()
    {
        var redisClient = _redisClient.GetConnectionMultiplexer();
        Assert.NotNull(redisClient);
    }
}