# NetCoreCache.Extensions.Redis

[![Build Status](https://github.com/sj-distributor/EasyCache/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/sj-distributor/EasyCache/actions?query=branch%3Amaster)
[![codecov](https://codecov.io/gh/sj-distributor/EasyCache/branch/master/graph/badge.svg?token=XV3W873RGV)](https://codecov.io/gh/sj-distributor/EasyCache)
[![NuGet version (EasyCache.ICore)](https://img.shields.io/nuget/v/EasyCache.ICore.svg?style=flat-square)](https://www.nuget.org/packages/EasyCache.ICore/)
![](https://img.shields.io/badge/license-MIT-green)

## 🔥Easily to use cache🔥

* InMemory Support
* Integrate into Redis caching
* Fast, concurrent, evicted memory, support big cache

## 🤟 Install
Choose caching provider that you need and install it via Nuget.
```
Install-Package EasyCache.InMemory
Install-Package EasyCache.Redis
```

## 🚀 Quick start

```C#
// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseServiceProviderFactory(new DynamicProxyServiceProviderFactory());
builder.Services.AddInMemoryCache(); // InMemory

// builder.Services.AddMultiBucketsInMemoryCache(); // Big cache
// builder.Services.AddRedisCache("server=localhost:6379;timeout=5000;MaxMessageSize=1024000;Expire=3600") // Redis 

// UserService.cs
[Cacheable("user-single", "{id}", 60 * 30)]
public virtual User Single(string id)
{
    return _dbContext.Set<User>().Single(x => x.Id == id);
}

**********************************
***** Method must be virtual *****
**********************************
```

## ⏱ About Cache expiration ( For InMemory )
* There are three ways of cache eviction, `LRU` and `TTL` and `Random`

## 🪣 About InMemory BigCache ( Multi Buckets )
* Fast. Performance scales on multi-core CPUs.
* The cache consists of many buckets, each with its own lock. This helps scaling the performance on multi-core CPUs,
  since multiple CPUs may concurrently access distinct buckets.
* `EasyCache.InMemory` automatically evicts old entries when reaching the maximum cache size set on its creation.


## 📌 Redis cluster

`builder.Services.AddRedisCache("server=127.0.0.1:6000,127.0.0.1:7000,127.0.0.1:6379;db=3;timeout=7000")`

```
This Redis component supports Redis Cluster, configure any node address, 
it will be able to automatically discover other addresses and slot distribution,
and use the correct node when performing read and write operations.

This Redis component does not directly support the Redis sentinel, but supports it in the form of active-standby failover.
```

## Cache automatic eviction

```C#
// UserService.cs
public class UserService
{
    [Cacheable("user-single", "{id}", 2)] // Cache expires after two seconds
    public virtual User Single(string id)
    {
        return _dbContext.Set<User>().Single(x => x.Id == id);
    }
}

```

## Active cache eviction

```C#
// UserService.cs
public class UserService
{
    [Cacheable("user-single", "{id}", 2)] // Cache expires after two seconds
    public virtual User Single(string id)
    {
        return _dbContext.Set<User>().Single(x => x.Id == id);
    }
    
    [Evictable(new[] { "user-single", "other cache name" }, "{id}")]
    public virtual void Delete(string id)
    {
        // delete logic...
    }
}

```

## 👻 Match both uses

```C#
// UserService.cs
public class UserService
{
    [Cacheable("user-single", "{user:id}", 60 * 30)]
    [Evictable(new[] { "user-single", "other cache name" }, "{user:id}")] // 
    public virtual User Update(User user)
    {
        // Update logic...
    }
}

**********************************************

The Update method will be executed first. 
After the method is successfully executed, the setup cache will be invalidated,
and finally the Cacheable operation will be executed.

**********************************************
```

## 🎃 Parameter Description

```c#
// MemoryCache
public static void AddInMemoryCache(
    this IServiceCollection services, 
    int maxCapacity = 1000000,
    MaxMemoryPolicy maxMemoryPolicy = MaxMemoryPolicy.LRU, int cleanUpPercentage = 10
)
{  // ... }

// MulitBucketsMemoryCache
public static void AddMultiBucketsInMemoryCache(
    this IServiceCollection services,
    uint buckets = 5,
    uint maxCapacity = 500000,
    MaxMemoryPolicy maxMemoryPolicy = MaxMemoryPolicy.LRU,
    int cleanUpPercentage = 10)
{  //...  }
```

|                          Parameter                           | Type |       Default       | Require | Explain                                                                                                                                     |
|:------------------------------------------------------------:|:----:|:-------------------:|:-------:|---------------------------------------------------------------------------------------------------------------------------------------------|
| `buckets` | uint | 5 | false | The number of containers to store the cache, up to 128                                                                                      |
|                     `bucketMaxCapacity`                      | uint |       1000000       |  false  | (MemoryCache) Initialize capacity <br/>  <br/> (MulitBucketsMemroyCache) The capacity of each barrel, it is recommended that 500,000 ~ 1,000,000 |
|                      `maxMemoryPolicy`                       | MaxMemoryPolicy | MaxMemoryPolicy.LRU |  false  | LRU = Least Recently Used , TTL = Time To Live, Or RANDOM                                                                                   |
|                     `cleanUpPercentage`                      | int |         10          |  false  | After the capacity is removed, the percentage deleted                                                                                       |  


## Variable explanation

```
// foo:bar:1 -> "item1"
{
   "foo": {
      "bar": [
         "item1",
         "qux"
     ]
   }
}

// foo:bar:0:url -> "test.weather.com"
{
   "foo": {
      "bar": [
         {
            "url": "test.weather.com",
            "key": "DEV1234567"
         }
     ]
   }
}
```

## Other works about caching 
[NetCoreCache( MemoryCache )](https://github.com/sj-distributor/core-cache) 👈🏻👈🏻
[NetCoreCacheRedis( RedisCache )](https://github.com/sj-distributor/core-cache.Extensions.Redis) 👈🏻👈🏻