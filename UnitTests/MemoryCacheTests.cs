using System;
using System.Threading;
using System.Threading.Tasks;
using FastCache.Core.Entity;
using FastCache.InMemory.Drivers;
using FastCache.InMemory.Enum;
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
                CreatedAt = DateTime.UtcNow.AddSeconds(i).Ticks,
                Expire = DateTime.UtcNow.AddHours(20).Ticks,
                Hits = (ulong)i + 1
            });
            for (var j = 0; j < i; j++)
            {
                memoryCache.Get($"{i}");
            }
        }

        Assert.Equal(50, memoryCache.GetBuckets().Count);
        memoryCache.Set("100", new CacheItem()
        {
            Value = 100
        });

        Assert.Equal(memoryCache.GetBuckets().Count, 50 - (50 / 10) + 1);
    }

    [Theory]
    [InlineData("anson1", "18", "18")]
    [InlineData("anson11", "19", "19")]
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
    [InlineData("anson2", "18", null)]
    [InlineData("anson22", "19", null)]
    [InlineData("anson22", null, null)]
    public async void TestMemoryCacheCanDelete(string key, string value, string result)
    {
        await _memoryCache.Set(key, new CacheItem()
        {
            Value = value,
            Expire = DateTime.UtcNow.AddSeconds(20).Ticks
        });
        await _memoryCache.Delete(key);
        var s = await _memoryCache.Get(key);
        Assert.Equal(s.Value, result);

        await _memoryCache.SetValue(key, new CacheItem()
        {
            Value = value,
            Expire = DateTime.UtcNow.AddSeconds(20).Ticks
        });
        await _memoryCache.Delete(key);
        s = await _memoryCache.GetValue(key);
        Assert.Equal(s.Value, result);
    }


    [Theory]
    [InlineData("", "anson1111", "18", null)]
    [InlineData("", "anson2222", "19", null)]
    public async void TestMemoryCacheCanDeleteByPattern(string prefix, string key, string value, string result)
    {
        await _memoryCache.Set(key, new CacheItem()
        {
            Value = value,
            Expire = DateTime.UtcNow.AddSeconds(20).Ticks
        });
        await _memoryCache.Delete("anson*", prefix);
        var s = await _memoryCache.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("anson", "anson1111", "18", null)]
    [InlineData("anson", "anson2222", "19", null)]
    public async void TestMemoryCacheCanDeleteByPatternWithPrefix(string prefix, string key, string value,
        string result)
    {
        var fullKey = $"{prefix}:{key}";
        await _memoryCache.Set(fullKey, new CacheItem()
        {
            Value = value,
            Expire = DateTime.UtcNow.AddSeconds(20).Ticks
        });
        await _memoryCache.Delete("anson*", prefix);
        var s = await _memoryCache.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("ansonExpire333", "18", null)]
    [InlineData("ansonExpire444", "19", null)]
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

    [Theory]
    [InlineData("", "ansonExpire333", "*anson", "18", null)]
    [InlineData("", "ansonExpire444", "anson*", "19", null)]
    [InlineData("", "ansonExpire555", "*", "20", null)]
    public async void TestMemoryCacheCanDeleteAsterisk(string prefix, string key, string deleteKey, string value,
        string result)
    {
        await _memoryCache.Set(key, new CacheItem()
        {
            Value = value,
            Expire = DateTime.UtcNow.AddSeconds(20).Ticks
        });
        await _memoryCache.Delete(deleteKey, prefix);
        var s = await _memoryCache.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("anson", "ansonExpire333", "*anson", "18", null)]
    [InlineData("anson", "ansonExpire444", "anson*", "19", null)]
    [InlineData("anson", "ansonExpire555", "*", "20", null)]
    public async void TestMemoryCacheCanDeleteAsteriskWithPrefix(string prefix, string key, string deleteKey,
        string value,
        string result)
    {
        var fullKey = $"{prefix}:{key}";
        await _memoryCache.Set(fullKey, new CacheItem()
        {
            Value = value,
            Expire = DateTime.UtcNow.AddSeconds(20).Ticks
        });
        await _memoryCache.Delete(deleteKey, prefix);
        var s = await _memoryCache.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("", "ansonExpire333", "*", "18", null)]
    [InlineData("", "ansonExpire444", "*", "19", null)]
    [InlineData("", "ansonExpire555", "*", "20", null)]
    public async void TestMemoryCacheCanDeleteByGeneralMatchAsterisk(string prefix, string key, string deleteKey,
        string value,
        string result)
    {
        await _memoryCache.Set(key, new CacheItem()
        {
            Value = value,
            Expire = DateTime.UtcNow.AddSeconds(20).Ticks
        });
        await _memoryCache.Delete(deleteKey, prefix);
        var s = await _memoryCache.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("anson", "ansonExpire333", "*", "18", null)]
    [InlineData("anson", "ansonExpire444", "*", "19", null)]
    [InlineData("anson", "ansonExpire555", "*", "20", null)]
    public async void TestMemoryCacheCanDeleteByGeneralMatchAsteriskWithPrefix(string prefix, string key,
        string deleteKey,
        string value,
        string result)
    {
        var fullKey = $"{prefix}:{key}";
        await _memoryCache.Set(fullKey, new CacheItem()
        {
            Value = value,
            Expire = DateTime.UtcNow.AddSeconds(20).Ticks
        });
        await _memoryCache.Delete(deleteKey, prefix);
        var s = await _memoryCache.Get(key);
        Assert.Equal(s.Value, result);
    }

    [Theory]
    [InlineData("anson", "ansonExpire333", "*", "18", null)]
    [InlineData("anson", "ansonExpire444", "*", "19", null)]
    [InlineData("anson", "ansonExpire555", "*", "20", null)]
    public async void TestMemoryCacheDoubleDelete(string prefix, string key,
        string deleteKey,
        string value,
        string result)
    {
        var fullKey = $"{prefix}:{key}";
        await _memoryCache.Set(fullKey, new CacheItem()
        {
            Value = value,
            Expire = DateTime.UtcNow.AddSeconds(20).Ticks
        });
        await _memoryCache.Delete(deleteKey, prefix);
        var s = await _memoryCache.Get(key);
        Assert.Equal(s.Value, result);

        await _memoryCache.Set(fullKey, new CacheItem()
        {
            Value = value,
            Expire = DateTime.UtcNow.AddSeconds(20).Ticks
        });

        await Task.Delay(TimeSpan.FromSeconds(4));

        s = await _memoryCache.Get(key);
        Assert.Equal(s.Value, result);
    }
}