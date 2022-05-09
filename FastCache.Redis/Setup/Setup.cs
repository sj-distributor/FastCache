using EasyCache.Core.Driver;
using EasyCache.Redis.Driver;
using Microsoft.Extensions.DependencyInjection;

namespace EasyCache.Redis.Setup
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