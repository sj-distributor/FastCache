#nullable enable
using Autofac;
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
        public static void RegisterMultiSourceCache(
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

        // 新增Autofac专用注册方式
        public static void RegisterMultiSourceCache(
            this ContainerBuilder builder,
            ConfigurationOptions configurationOptions,
            RedisCacheOptions? redisCacheOptions = null,
            MemoryCacheOptions? memoryCacheOptions = null)
        {
            var memoryOpts = memoryCacheOptions ?? new MemoryCacheOptions();
            var redisOpts = redisCacheOptions ?? new RedisCacheOptions();

            builder.Register(ctx => new RedisCache(configurationOptions, redisOpts))
                .As<IRedisCache>()
                .SingleInstance();

            builder.Register(ctx => new MemoryCache(
                    memoryOpts.MaxCapacity,
                    memoryOpts.MemoryPolicy,
                    memoryOpts.CleanUpPercentage))
                .As<IMemoryCache>()
                .SingleInstance();
        }
    }
}