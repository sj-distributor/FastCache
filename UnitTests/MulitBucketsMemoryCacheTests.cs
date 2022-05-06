using System;
using System.Threading;
using EasyCache.Core.Entity;
using EasyCache.Enum;
using EasyCache.InMemory.Drivers;
using Xunit;

namespace UnitTests;

public class MultiBucketsMemoryCacheTests
{
    private readonly MultiBucketsMemoryCache _memoryCache;

    public MultiBucketsMemoryCacheTests()
    {
        _memoryCache = new MultiBucketsMemoryCache();
    }

    [Theory]
    [InlineData(MaxMemoryPolicy.LRU)]
    [InlineData(MaxMemoryPolicy.TTL)]
    [InlineData(MaxMemoryPolicy.RANDOM)]
    public void TestWhenTheMemoryIsFull_EliminatedSuccess(MaxMemoryPolicy maxMemoryPolicy)
    {
        var memoryCache = new MultiBucketsMemoryCache(1, 50);
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

        memoryCache.GetBuckets().TryGetValue(0, out var bucket);


        Assert.Equal(50, bucket.Count);
        memoryCache.Set("100", new CacheItem()
        {
            Value = 100
        });
        Assert.Equal(bucket.Count, 50 - (50 / 10) + 1);
    }

    [Theory]
    [InlineData("anson4", "18", "18")]
    [InlineData("anson44", "19", "19")]
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
    [InlineData("anson5", "18", null)]
    [InlineData("anson55", "19", null)]
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
    [InlineData("anson555", "18", null)]
    [InlineData("anson555555", "19", null)]
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
    [InlineData("ansonExpire11", "18", null)]
    [InlineData("ansonExpire22", "19", null)]
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