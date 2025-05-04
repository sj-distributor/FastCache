#nullable enable
using FastCache.Core.Driver;
using FastCache.Core.Entity;
using FastCache.InMemory.Drivers;
using FastCache.Redis.Driver;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace FastCache.MultiSource.Setup
{
    public static class Setup
    {
        public static void AddMultiSourceCache(
            this IServiceCollection services,
            ConfigurationOptions configurationOptions,
            RedisCacheOptions? redisCacheOptions = null,
            MemoryCacheOptions? memoryCacheOptions = null
        )
        {
            var memoryCacheOption = memoryCacheOptions ?? new MemoryCacheOptions();
            var redisCacheOption = redisCacheOptions ?? new RedisCacheOptions();

            services.AddSingleton<IRedisCache>(new RedisCache(configurationOptions, redisCacheOption));
            services.AddSingleton<IMemoryCache>(new MemoryCache(memoryCacheOption.MaxCapacity,
                memoryCacheOption.MemoryPolicy, memoryCacheOption.CleanUpPercentage));
        }
    }
}