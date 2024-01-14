using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AspectCore.DynamicProxy;
using FastCache.Core.Driver;
using FastCache.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace FastCache.Core.Attributes
{
    public class EvictableAttribute : AbstractInterceptorAttribute
    {
        private readonly string[] _keys;
        private readonly string _expression;

        public sealed override int Order { get; set; }

        public EvictableAttribute(string[] keys, string expression)
        {
            _keys = keys;
            _expression = expression;
            Order = 3;
        }

        public override async Task Invoke(AspectContext context, AspectDelegate next)
        {
            await next(context);

            var cacheClient = context.ServiceProvider.GetService<ICacheClient>();

            var dictionary = new Dictionary<string, object>();
            var parameterInfos = context.ImplementationMethod.GetParameters();
            for (var i = 0; i < context.Parameters.Length; i++)
            {
                dictionary.Add(parameterInfos[i].Name, context.Parameters[i]);
            }

            foreach (var s in _keys)
            {
                await cacheClient.Delete(KeyGenerateHelper.GetKey(_expression, dictionary), s);
            }
        }
    }
}