using FastCache.Core.Driver;
using FastCache.InMemory.Drivers;
using FastCache.InMemory.Enum;
using FastCache.Redis.Driver;
using Microsoft.Extensions.DependencyInjection;

namespace FastCache.MultiSource.Setup
{
    public static class Setup
    {
        public static void AddMultiSourceCache(
            this IServiceCollection services,
            string connectionString,
            bool canGetRedisClient = false,
            int maxCapacity = 1000000,
            MaxMemoryPolicy maxMemoryPolicy = MaxMemoryPolicy.LRU, int cleanUpPercentage = 10
        )
        {
            services.AddSingleton<IRedisCache>(new RedisCache(connectionString, canGetRedisClient));
            services.AddSingleton<IMemoryCache>(new MemoryCache(maxCapacity, maxMemoryPolicy, cleanUpPercentage));
        }
    }
}