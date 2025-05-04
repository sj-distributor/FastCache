using FastCache.Core.Driver;
using FastCache.Redis.Driver;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace FastCache.Redis.Setup
{
    public static class Setup
    {
        public static void AddRedisCache(
            this IServiceCollection services,
            ConfigurationOptions configurationOptions
        )
        {
            services.AddSingleton<ICacheClient>(new RedisCache(configurationOptions));
        }
    }
}