using System;
using System.Threading;
using EasyCache.Core.Entity;
using EasyCache.InMemory.Drivers;
using EasyCache.InMemory.Enum;
using Xunit;

namespace UnitTests;

public class MemoryCacheTests
{
    public MemoryCache _memoryCache;

    public MemoryCacheTests()
    {
        _memoryCache = new MemoryCache(50, MaxMemoryPolicy.TTL);
    }

    [Theory]
    [InlineData(MaxMemoryPolicy.LRU)]
    [InlineData(MaxMemoryPolicy.TTL)]
    [InlineData(MaxMemoryPolicy.RANDOM)]
    public void TestWhenTheMemoryIsFull_EliminatedSuccess(MaxMemoryPolicy maxMemoryPolicy)
    {
        var memoryCache = new MemoryCache(50, maxMemoryPolicy);
        for (var i = 0; i < 50; i++)
        {
            Thread.Sleep(TimeSpan.FromSeconds(0.1));
            memoryCache.Set($"{i}", new CacheItem()
            {
                Value = i,
                CreatedAt = DateTime.Now.AddSeconds(i).Ticks,
                Expire = DateTime.Now.AddHours(20).Ticks,
                Hits = (ulong)i + 1
            });
            for (var j = 0; j < i; j++)
            {
                memoryCache.Get($"{i}");
            }
        }

        Assert.Equal(memoryCache.GetBuckets().Count, 50);
        memoryCache.Set("100", new CacheItem()
        {
            Value = 100
        });

        Assert.Equal(memoryCache.GetBuckets().Count, 50 - (50 / 10) + 1);
    }

    [Theory]
    [InlineData("anson", "18", "18")]
    [InlineData("anson1", "19", "19")]
    public async void TestMemoryCacheCanSet(string key, string value, string result)
    {
        await _memoryCache.Set(key, new CacheItem()
        {
            Value = value,
            Expire = DateTime.Now.AddSeconds(20).Ticks
        });
        var s = await _memoryCache.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("anson", "18", null)]
    [InlineData("anson1", "19", null)]
    public async void TestMemoryCacheCanDelete(string key, string value, string result)
    {
        await _memoryCache.Set(key, new CacheItem()
        {
            Value = value,
            Expire = DateTime.Now.AddSeconds(20).Ticks
        });
        await _memoryCache.Delete(key);
        var s = await _memoryCache.Get(key);
        Assert.Equal(s.Value, result);
    }


    [Theory]
    [InlineData("anson1111", "18", null)]
    [InlineData("anson2222", "19", null)]
    public async void TestMemoryCacheCanDeleteByPattern(string key, string value, string result)
    {
        await _memoryCache.Set(key, new CacheItem()
        {
            Value = value,
            Expire = DateTime.Now.AddSeconds(20).Ticks
        });
        await _memoryCache.Delete("anson*");
        var s = await _memoryCache.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("ansonExpire", "18", null)]
    [InlineData("ansonExpire", "19", null)]
    public async void TestMemoryCacheCanExpire(string key, string value, string result)
    {
        await _memoryCache.Set(key, new CacheItem()
        {
            Value = value,
            Expire = 1
        });
        Thread.Sleep(TimeSpan.FromSeconds(1.1));
        var cacheItem = await _memoryCache.Get(key);
        Assert.Equal(cacheItem.Value, result);
    }
}