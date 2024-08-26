using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AspectCore.DynamicProxy;
using FastCache.Core.Driver;
using FastCache.Core.Enums;
using FastCache.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace FastCache.MultiSource.Attributes
{
    public class MultiSourceEvictableAttribute : AbstractInterceptorAttribute
    {
        private readonly string[] _keys;
        private readonly string _expression;
        private readonly Target _target;
        private readonly int _msTimeout;
        private readonly int _msExpire;
        private readonly bool _throwOnFailure;

        public sealed override int Order { get; set; }

        public MultiSourceEvictableAttribute(string[] keys, string expression, Target target, int msTimeout = 100,
            int msExpire = 1000,
            bool throwOnFailure = false)
        {
            _keys = keys;
            _expression = expression;
            _target = target;
            _msTimeout = msTimeout;
            _msExpire = msExpire;
            _throwOnFailure = throwOnFailure;
            Order = 3;
        }

        public override async Task Invoke(AspectContext context, AspectDelegate next)
        {
            await next(context);

            ICacheClient cacheClient;

            switch (_target)
            {
                case Target.Redis:
                    cacheClient = context.ServiceProvider.GetService<IRedisCache>();
                    break;
                case Target.InMemory:
                    cacheClient = context.ServiceProvider.GetService<IMemoryCache>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var dictionary = new Dictionary<string, object>();
            var parameterInfos = context.ImplementationMethod.GetParameters();
            for (var i = 0; i < context.Parameters.Length; i++)
            {
                dictionary.Add(parameterInfos[i].Name, context.Parameters[i]);
            }

            var isRedis = _target == Target.Redis;

            foreach (var s in _keys)
            {
                var key = KeyGenerateHelper.GetKey(_expression, dictionary);

                if (isRedis)
                {
                    await cacheClient.DeleteAsyncLock(key, s, _msTimeout, _msExpire, _throwOnFailure);
                }
                else
                {
                    await cacheClient.Delete(key, s);
                }
            }
        }
    }
}