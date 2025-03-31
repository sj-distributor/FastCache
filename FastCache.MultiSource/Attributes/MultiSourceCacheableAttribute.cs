using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AspectCore.DynamicProxy;
using FastCache.Core.Driver;
using FastCache.Core.Entity;
using FastCache.Core.Enums;
using FastCache.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace FastCache.MultiSource.Attributes
{
    public class MultiSourceCacheableAttribute : AbstractInterceptorAttribute
    {
        private readonly string _key;
        private readonly string _expression;
        private readonly Target _target;
        private readonly long _expire;
        public sealed override int Order { get; set; }

        private static readonly ConcurrentDictionary<Type, MethodInfo>
            TypeofTaskResultMethod = new ConcurrentDictionary<Type, MethodInfo>();

        private static readonly MethodInfo TaskResultMethod;

        static MultiSourceCacheableAttribute()
        {
            TaskResultMethod = typeof(Task).GetMethods()
                .First(p => p.Name == "FromResult" && p.ContainsGenericParameters);
        }

        public MultiSourceCacheableAttribute(string key, string expression, Target target, long expireSeconds = 0)
        {
            _key = key;
            _expression = expression;
            _target = target;
            _expire = expireSeconds;
            Order = 2;
        }

        public override async Task Invoke(AspectContext context, AspectDelegate next)
        {
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

            var key = KeyGenerateHelper.GetKey(_key, _expression, dictionary);

            var methodInfo = context.Implementation.GetType().GetMethods()
                .First(x => x.Name == context.ProxyMethod.Name);

            var canGetCache = true;

            if (methodInfo.CustomAttributes.Any())
            {
                if (methodInfo.CustomAttributes.Any(customAttributeData =>
                        customAttributeData.AttributeType.FullName == typeof(MultiSourceEvictableAttribute).FullName))
                {
                    canGetCache = false;
                }
            }

            if (canGetCache)
            {
                if (context.ProxyMethod.CustomAttributes.Any(customAttribute =>
                        customAttribute.AttributeType.FullName == typeof(MultiSourceEvictableAttribute).FullName))
                {
                    canGetCache = false;
                }
            }

            if (canGetCache)
            {
                var cacheValue = await cacheClient.Get(key);

                if (null != cacheValue.Value && cacheValue.AssemblyName != null && cacheValue.Type != null)
                {
                    var returnTypeBefore = context.IsAsync()
                        ? context.ServiceMethod.ReturnType.GetGenericArguments().First()
                        : context.ServiceMethod.ReturnType;

                    context.ReturnValue = context.IsAsync()
                        ? TypeofTaskResultMethod.GetOrAdd(returnTypeBefore,
                                t => TaskResultMethod.MakeGenericMethod(returnTypeBefore))
                            .Invoke(null, new[] { cacheValue.Value })
                        : cacheValue.Value;

                    return;
                }
            }

            await next(context);

            object value;

            if (context.IsAsync())
            {
                value = await context.UnwrapAsyncReturnValue();
            }
            else
            {
                value = context.ReturnValue;
            }

            var returnType = value.GetType();

            await cacheClient.Set(key, new CacheItem
            {
                Value = value,
                CreatedAt = DateTime.UtcNow.Ticks,
                Expire =
                    _expire > 0 ? DateTime.UtcNow.AddSeconds(_expire).Ticks : DateTime.UtcNow.AddYears(1).Ticks,
                AssemblyName = returnType.Assembly.GetName().FullName,
                Type = returnType.FullName ?? string.Empty,
            }, _expire);
        }
    }
}