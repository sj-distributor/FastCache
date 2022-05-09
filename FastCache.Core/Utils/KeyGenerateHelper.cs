#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace EasyCache.Core.Utils
{
    public static class KeyGenerateHelper
    {
        public static string GetKey(string name, string originKey, IDictionary<string, object>? parameters)
        {
            var values = new ConfigurationBuilder()
                .AddJsonStream(
                    new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(parameters))))
                .Build();

            var reg = new Regex("\\{([^\\}]*)\\}");
            var matches = reg.Matches(originKey);

            foreach (Match match in matches)
            {
                originKey = originKey.Replace(match.Value, values[match.Value.Replace(@"{", "").Replace(@"}", "")]);
            }

            return $"{name}:{originKey}";
        }
    } 
}

