using System.Collections.Generic;
using System.Threading.Tasks;
using AspectCore.DynamicProxy;
using FastCache.Core.Driver;
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