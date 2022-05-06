using EasyCache.Core.Driver;
using EasyCache.InMemory.Drivers;
using EasyCache.InMemory.Enum;
using Microsoft.Extensions.DependencyInjection;

namespace EasyCache.InMemory.Setup
{
    public static class Setup
    {
        public static void AddInMemoryCache(
            this IServiceCollection services, 
            int maxCapacity = 1000000,
            MaxMemoryPolicy maxMemoryPolicy = MaxMemoryPolicy.LRU, int cleanUpPercentage = 10
        )
        {
            services.AddSingleton<ICacheClient>(new MemoryCache(maxCapacity, maxMemoryPolicy, cleanUpPercentage));
        }
        
        public static void AddMultiBucketsInMemoryCache(
            this IServiceCollection services,
            uint buckets = 5,
            uint maxCapacity = 500000,
            MaxMemoryPolicy maxMemoryPolicy = MaxMemoryPolicy.LRU,
            int cleanUpPercentage = 10)
        {
            services.AddSingleton<ICacheClient>(
                new MultiBucketsMemoryCache(buckets, maxCapacity, maxMemoryPolicy, cleanUpPercentage));
        }
    }
}