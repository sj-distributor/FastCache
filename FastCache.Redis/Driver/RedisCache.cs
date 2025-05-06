using System;
using System.Collections.Generic;
using System.Linq;
using FastCache.Core.Driver;
using FastCache.Core.Entity;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;

namespace FastCache.Redis.Driver
{
    public partial class RedisCache : IRedisCache
    {
        private RedLockFactory _redLockFactory;

        private readonly ConnectionMultiplexer _redisConnection;

        private readonly List<EventHandler<ConnectionFailedEventArgs>> _eventHandlers =
            new List<EventHandler<ConnectionFailedEventArgs>>();

        public ConnectionMultiplexer GetConnectionMultiplexer()
        {
            return _redisConnection;
        }

        public RedLockFactory GetRedLockFactory()
        {
            return _redLockFactory;
        }

        public RedisCache(ConfigurationOptions configuration, RedisCacheOptions? redisCacheOptions = null,
            Action<object?, ConnectionFailedEventArgs>? customHandler = null)
        {
            if (configuration == null)
                throw new ArgumentNullException(
                    paramName: nameof(configuration),
                    message: "Redis configuration cannot be null. Please provide valid ConfigurationOptions");

            if (configuration.EndPoints.Count == 0)
            {
                throw new ArgumentException(
                    message: "At least one Redis endpoint must be configured",
                    paramName: nameof(configuration));
            }

            var option = redisCacheOptions ?? new RedisCacheOptions();

            _redisConnection = ConnectionMultiplexer.Connect(configuration);

            if (_redisConnection == null)
                throw new InvalidOperationException();

            SetupRedisLockFactory(new List<ConnectionMultiplexer>() { _redisConnection }, option);

            RegisterRedisConnectionFailure(_redisConnection, customHandler);

            RegisterRedisConnectionRestoredHandler(_redisConnection, customHandler);
        }

        private void RegisterRedisConnectionFailure(ConnectionMultiplexer connectionMultiplexer,
            Action<object?, ConnectionFailedEventArgs>? customHandler = null)
        {
            EventHandler<ConnectionFailedEventArgs> handler = (sender, args) =>
            {
                var logMessage = $"[Redis连接失败] " +
                                 $"类型: {args.FailureType}, " +
                                 $"端点: {args.EndPoint}, " +
                                 $"异常: {args.Exception?.GetBaseException().Message}";

                Console.WriteLine(logMessage);

                customHandler?.Invoke(sender, args);
            };

            connectionMultiplexer.ConnectionFailed += handler;

            _eventHandlers.Add(handler);
        }

        private void RegisterRedisConnectionRestoredHandler(ConnectionMultiplexer connectionMultiplexer,
            Action<object?, ConnectionFailedEventArgs>? customHandler = null)
        {
            EventHandler<ConnectionFailedEventArgs> handler = (sender, args) =>
            {
                Console.WriteLine($"[连接恢复]");

                // 执行自定义处理
                customHandler?.Invoke(sender, args);
            };

            connectionMultiplexer.ConnectionRestored += handler;

            _eventHandlers.Add(handler);
        }

        private void SetupRedisLockFactory(List<ConnectionMultiplexer> connectionMultiplexers,
            RedisCacheOptions redisCacheOptions)
        {
            var redLockMultiplexers = connectionMultiplexers
                .Select(connectionMultiplexer => (RedLockMultiplexer)connectionMultiplexer).ToList();

            _redLockFactory = RedLockFactory.Create(redLockMultiplexers,
                new RedLockRetryConfiguration(retryCount: redisCacheOptions.QuorumRetryCount,
                    retryDelayMs: redisCacheOptions.QuorumRetryDelayMs));
        }

        public void Dispose()
        {
            foreach (var handler in _eventHandlers)
            {
                _redisConnection.ConnectionFailed -= handler;
            }

            _redisConnection?.Dispose();
            _redLockFactory?.Dispose();
        }
    }
}