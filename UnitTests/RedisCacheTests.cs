using System;
using System.Threading.Tasks;
using FastCache.Core.Entity;
using FastCache.Redis.Driver;
using Xunit;

namespace UnitTests;

public class RedisCacheTests
{
    private readonly RedisCache _redisClient =
        new("server=localhost:6379;timeout=5000;MaxMessageSize=1024000;Expire=3600", true);

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


    [Fact]
    public void TestCanGetRedisClient()
    {
        var redisClient = _redisClient.GetRedisClient();
        Assert.NotNull(redisClient);
    }
}