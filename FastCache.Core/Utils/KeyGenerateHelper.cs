using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace FastCache.Core.Utils
{
    public static class KeyGenerateHelper
    {
        public static string GetKey(string name, string originKey, IDictionary<string, object>? parameters)
        {
            var values = new ConfigurationBuilder()
                .AddJsonStream(
                    new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(parameters))))
                .Build();

            var reg = new Regex(@"\{([^\}]*)\}");
            var matches = reg.Matches(originKey);

            foreach (Match match in matches)
            {
                var valueName = match.Value.Replace(@"{", "").Replace(@"}", "");
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

            return $"{name}:{originKey}";
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
                var valueName = match.Value.Replace(@"{", "").Replace(@"}", "");
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

            return originKey;
        }
    }
}