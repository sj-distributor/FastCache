using System;
using System.Threading.Tasks;
using EasyCache.Core.Entity;
using EasyCache.Redis.Driver;
using Xunit;

namespace UnitTests;

public class RedisCacheTests
{
    private RedisCache _redisClient;

    public RedisCacheTests()
    {
        _redisClient = new RedisCache("server=localhost:6379;timeout=5000;MaxMessageSize=1024000;Expire=3600");
    }

    [Theory]
    [InlineData("anson", "18", "18")]
    [InlineData("anson1", "19", "19")]
    public async void TestRedisCacheCanSet(string key, string value, string result)
    {
        await _redisClient.Set(key, new CacheItem()
        {
            Value = value,
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
        });
        await _redisClient.Delete(key);
        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("anson1111", "18", null)]
    [InlineData("anson2222", "19", null)]
    public async void TestRedisCacheCanDeleteByLastPattern(string key, string value, string result)
    {
        await _redisClient.Set(key, new CacheItem()
        {
            Value = value,
        });
        await _redisClient.Delete("anson*");
        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }
    
    [Theory]
    [InlineData("1111Joe", "18", null)]
    [InlineData("2222Joe", "19", null)]
    public async void TestRedisCacheCanDeleteByFirstPattern(string key, string value, string result)
    {
        await _redisClient.Set(key, new CacheItem()
        {
            Value = value,
        });
        await _redisClient.Delete("*Joe");
        var s = await _redisClient.Get(key);
        Assert.Equal(s.Value, result);
    }
}