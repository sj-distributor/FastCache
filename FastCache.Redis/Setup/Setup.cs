using FastCache.Core.Driver;
using FastCache.Redis.Driver;
using Microsoft.Extensions.DependencyInjection;

namespace FastCache.Redis.Setup
{
    public static class Setup
    {
        public static void AddRedisCache(
            this IServiceCollection services,
            string connectionString,
            bool canGetRedisClient = false
        )
        {
            services.AddSingleton<ICacheClient>(new RedisCache(connectionString, canGetRedisClient));
        }
    }
}