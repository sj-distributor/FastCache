using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace FastCache.Core.Utils
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
    public static class KeyGenerateHelper
    {
        public static string GetKey(string name, string originKey, IDictionary<string, object>? parameters)
        {
            var valueKey = GetKey(originKey, parameters);

            return $"{name}:{valueKey}";
        }

        public static string GetKey(string originKey, IDictionary<string, object>? parameters)
        {
            var values = new ConfigurationBuilder()
                .AddJsonStream(
                    new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(parameters))))
                .Build();

            var reg = new Regex(@"\{([^\}]*)\}");
            var matches = reg.Matches(originKey);

            foreach (Match match in matches)
            {
                var valueName = match.Value.Replace("{", "").Replace("}", "");
                if (valueName.Contains(":all"))
                {
                    var modifiedPattern = valueName.Replace(":all", "");

                    var lastColonIndex = modifiedPattern.LastIndexOf(":", StringComparison.Ordinal);

                    if (lastColonIndex == -1)
                    {
                        originKey = originKey.Replace(match.Value, "");
                        continue;
                    }

                    var regexPattern = "^" + Regex.Escape(modifiedPattern[..lastColonIndex])
                                           + @":[^:]+:" // 确保中间只允许一个非冒号的值
                                           + Regex.Escape(modifiedPattern[(lastColonIndex + 1)..]) + "$";

                    // 获取所有符合条件的值
                    var matchValues = values.AsEnumerable().Reverse().ToList()
                        .Where(x => Regex.IsMatch(x.Key, regexPattern, RegexOptions.IgnoreCase)) // 匹配路径
                        .Where(x => !string.IsNullOrWhiteSpace(x.Value)) // 确保值不为空白
                        .Select(x => x.Value) // 提取值
                        .ToList();

                    // 用匹配到的值更新 originKey
                    originKey = originKey.Replace(match.Value, matchValues.Any() ? string.Join(",", matchValues) : "");
                }
                else
                {
                    // Handle normal case
                    var sections = values.GetSection(valueName).GetChildren()
                        .Where(x => !string.IsNullOrEmpty(x.Value))
                        .ToList();

                    if (sections.Any())
                    {
                        var valuesList = sections.Select(keyValuePair => keyValuePair.Value).ToList();
                        originKey = originKey.Replace(match.Value, string.Join(",", valuesList));
                    }
                    else
                    {
                        originKey = originKey.Replace(match.Value, values[valueName]);
                    }
                }
            }

            return originKey;
        }
    }
}