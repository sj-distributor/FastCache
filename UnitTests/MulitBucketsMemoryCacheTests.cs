using System;
using System.Threading;
using FastCache.Core.Entity;
using FastCache.Core.Enums;
using FastCache.InMemory.Drivers;
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
        var memoryCache = new MultiBucketsMemoryCache(1, 50, maxMemoryPolicy);
        for (var i = 0; i < 50; i++)
        {
            Thread.Sleep(TimeSpan.FromSeconds(0.1));
            memoryCache.Set($"{i}", new CacheItem()
            {
                Value = i,
                CreatedAt = DateTime.UtcNow.AddSeconds(i).Ticks,
                Expire = DateTime.UtcNow.AddHours(20).Ticks,
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
            Type = value.GetType().FullName,
            AssemblyName = value.GetType().Assembly.FullName,
            Value = value,
            Expire = DateTime.UtcNow.AddSeconds(20).Ticks
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
            Type = value.GetType().FullName,
            AssemblyName = value.GetType().Assembly.FullName,
            Value = value,
            Expire = DateTime.UtcNow.AddSeconds(20).Ticks
        });
        await _memoryCache.Delete(key);
        var s = await _memoryCache.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("", "anson555", "18", null)]
    [InlineData("", "anson555555", "19", null)]
    public async void TestMemoryCacheCanDeleteByFirstPattern(string prefix, string key, string value, string result)
    {
        await _memoryCache.Set(key, new CacheItem()
        {
            Type = value.GetType().FullName,
            AssemblyName = value.GetType().Assembly.FullName,
            Value = value,
            Expire = DateTime.UtcNow.AddSeconds(20).Ticks
        });
        await _memoryCache.Delete("anson*", prefix);
        var s = await _memoryCache.Get(key);
        Assert.Equal(s.Value, result);
    }
    
    [Theory]
    [InlineData("anson", "anson555", "18", null)]
    [InlineData("anson", "anson555555", "19", null)]
    public async void TestMemoryCacheCanDeleteByFirstPatternWithPrefix(string prefix, string key, string value, string result)
    {
        var fullKey = $"{prefix}:{key}";
        await _memoryCache.Set(fullKey, new CacheItem()
        {
            Type = value.GetType().FullName,
            AssemblyName = value.GetType().Assembly.FullName,
            Value = value,
            Expire = DateTime.UtcNow.AddSeconds(20).Ticks
        });
        await _memoryCache.Delete("anson*", prefix);
        var s = await _memoryCache.Get(key);
        Assert.Equal(s.Value, result);
    }


    [Theory]
    [InlineData("", "555Joe", "18", null)]
    [InlineData("", "555555Joe", "19", null)]
    public async void TestMemoryCacheCanDeleteByLastPattern(string prefix, string key, string value, string result)
    {
        await _memoryCache.Set(key, new CacheItem()
        {
            Type = value.GetType().FullName,
            AssemblyName = value.GetType().Assembly.FullName,
            Value = value,
            Expire = DateTime.UtcNow.AddSeconds(20).Ticks
        });
        await _memoryCache.Delete("*Joe", prefix);
        var s = await _memoryCache.Get(key);
        Assert.Equal(s.Value, result);
    }


    [Theory]
    [InlineData("Joe", "555Joe", "18", null)]
    [InlineData("Joe", "555555Joe", "19", null)]
    public async void TestMemoryCacheCanDeleteByLastPatternWithPrefix(string prefix, string key, string value,
        string result)
    {
        var fullKey = $"{prefix}:{key}";
        await _memoryCache.Set(fullKey, new CacheItem()
        {
            Type = value.GetType().FullName,
            AssemblyName = value.GetType().Assembly.FullName,
            Value = value,
            Expire = DateTime.UtcNow.AddSeconds(20).Ticks
        });
        await _memoryCache.Delete("*Joe", prefix);
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
            Type = value.GetType().FullName,
            AssemblyName = value.GetType().Assembly.FullName,
            Value = value,
            Expire = 1
        });
        Thread.Sleep(TimeSpan.FromSeconds(1.1));
        var cacheItem = await _memoryCache.Get(key);
        Assert.Equal(cacheItem.Value, result);
    }
}