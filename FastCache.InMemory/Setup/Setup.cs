using FastCache.Core.Driver;
using FastCache.Core.Entity;
using FastCache.Core.Enums;
using FastCache.InMemory.Drivers;
using Microsoft.Extensions.DependencyInjection;

namespace FastCache.InMemory.Setup
{
    public static class Setup
    {
        public static void AddInMemoryCache(
            this IServiceCollection services,
            MemoryCacheOptions? memoryCacheOptions = null
        )
        {
            var option = memoryCacheOptions ?? new MemoryCacheOptions();

            services.AddSingleton<ICacheClient>(
                new MemoryCache(
                    maxCapacity: option.MaxCapacity, maxMemoryPolicy: option.MemoryPolicy,
                    cleanUpPercentage: option.CleanUpPercentage,
                    delaySeconds: option.DelaySeconds
                )
            );
        }

        public static void AddMultiBucketsInMemoryCache(
            this IServiceCollection services,
            uint buckets = 5,
            uint maxCapacity = 500000,
            MaxMemoryPolicy maxMemoryPolicy = MaxMemoryPolicy.LRU,
            int cleanUpPercentage = 10, int delaySeconds = 2)
        {
            services.AddSingleton<ICacheClient>(
                new MultiBucketsMemoryCache(
                    buckets, maxCapacity, maxMemoryPolicy, cleanUpPercentage,
                    delaySeconds: delaySeconds));
        }
    }
}