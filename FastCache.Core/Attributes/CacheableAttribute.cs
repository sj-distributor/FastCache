using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AspectCore.DynamicProxy;
using FastCache.Core.Driver;
using FastCache.Core.Entity;
using FastCache.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace FastCache.Core.Attributes
{
    /// <summary>
    /// 规则支持说明
    /// Based on the provided test cases, the `KeyGenerateHelper.GetKey` method supports the following rules for generating cache keys:
    /// Supported Rules
    ///  1. **Basic Property Access**:
    /// - `{company:name}`: Accesses the `Name` property of the `Company` object.
    ///  - `{company:id}`: Accesses the `Id` property.
    ///  2. **List and Array Indexing**:
    ///  - `{company:menus:0:openTime}`: Accesses the `openTime` of the first menu in the `Menus` list.
    ///  - `{company:merchants:0:merchantIds:0}`: Accesses the first `MerchantId` of the first merchant.
    ///  3. **Iterating All Elements**:
    ///  - `{company:menus:id:all}`: Joins all `Id`s from the `Menus` list.
    ///  - `{company:menus:0:menuSettings:id:all}`: Joins all `Id`s from `MenuSettings` of the first menu.
    ///  4. **Combining Multiple Properties**:
    ///  - `{company:name}:{company:id}`: Combines multiple properties into a single key.
    ///  - `{company:menus:id:all}:{company:id}`: Combines all menu IDs with company ID.
    /// 5. **Wildcard for All Elements**:
    ///  - `{company:all}:{company:id}` or similar patterns can be used to indicate special handling, though specifics depend on implementation details not provided here.
    /// General Structure
    /// - Prefix is added at the beginning (`single:`).
    /// - Patterns within curly braces are replaced with corresponding values from object properties or collections.
    ///  - Supports indexing and iteration over lists/arrays using specific indices or "all" for concatenation.
    ///  - Combinations of different patterns are supported to form complex keys.
    /// Implementation Notes
    ///  Ensure that your method correctly interprets these patterns and handles edge cases, such as missing data or empty lists, to prevent errors like null reference exceptions.
    /// </summary>
    public class CacheableAttribute : AbstractInterceptorAttribute
    {
        private readonly string _key;
        private readonly string _expression;
        private readonly TimeSpan _expire;

        public sealed override int Order { get; set; }

        private static readonly ConcurrentDictionary<Type, MethodInfo>
            TypeofTaskResultMethod = new ConcurrentDictionary<Type, MethodInfo>();

        private static readonly MethodInfo _taskResultMethod;

        static CacheableAttribute()
        {
            _taskResultMethod = typeof(Task).GetMethods()
                .First(p => p.Name == "FromResult" && p.ContainsGenericParameters);
        }
        
        // 支持秒数的构造器（保持向后兼容）
        public CacheableAttribute(string prefix, string keyPattern, int expirationSeconds)
            : this(prefix, keyPattern, TimeSpan.FromSeconds(expirationSeconds))
        {
        }

        // 新增支持TimeSpan的构造器
        public CacheableAttribute(string prefix, string keyPattern, double expirationMinutes)
            : this(prefix, keyPattern, TimeSpan.FromMinutes(expirationMinutes))
        {
        }

        public CacheableAttribute(string key, string expression, TimeSpan expireSeconds = default)
        {
            _key = key;
            _expression = expression;
            _expire = expireSeconds;
            Order = 2;
        }

        public override async Task Invoke(AspectContext context, AspectDelegate next)
        {
            var cacheClient = context.ServiceProvider.GetService<ICacheClient>();

            var dictionary = new Dictionary<string, object>();
            var parameterInfos = context.ImplementationMethod.GetParameters();
            for (var i = 0; i < context.Parameters.Length; i++)
            {
                dictionary.Add(parameterInfos[i].Name, context.Parameters[i]);
            }

            var key = KeyGenerateHelper.GetKey(_key, _expression, dictionary);

            var methodInfo = context.ImplementationMethod;

            var canGetCache = true;

            if (methodInfo.CustomAttributes.Any())
            {
                if (methodInfo.CustomAttributes.Any(customAttributeData =>
                        customAttributeData.AttributeType.FullName == typeof(EvictableAttribute).FullName))
                {
                    canGetCache = false;
                }
            }

            if (canGetCache)
            {
                if (context.ProxyMethod.CustomAttributes.Any(customAttribute =>
                        customAttribute.AttributeType.FullName == typeof(EvictableAttribute).FullName))
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
                                t => _taskResultMethod.MakeGenericMethod(returnTypeBefore))
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

            var returnType = value?.GetType();

            var expire = 0l;

            await cacheClient.Set(key, new CacheItem
            {
                Value = value,
                CreatedAt = DateTime.UtcNow.Ticks,
                Expire = _expire > TimeSpan.Zero
                    ? DateTime.UtcNow.Add(_expire).Ticks
                    : DateTime.UtcNow.AddYears(1).Ticks,
                AssemblyName = returnType?.Assembly?.GetName()?.FullName ?? typeof(string).Assembly.FullName,
                Type = returnType?.FullName ?? string.Empty,
            }, _expire);
        }
    }
}