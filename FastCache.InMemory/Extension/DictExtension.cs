using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace FastCache.InMemory.Extension
{
    public static class DictExtension
    {
        public static bool TryRemove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key,
            out TValue value, int seconds = 2)
        {
            var res = dictionary.TryRemove(key, out value);
            if (res && seconds > 0)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(seconds));
                    lock (dictionary)
                    {
                        dictionary.TryRemove(key, out var _);
                    }
                });
            }
            return res;
        }
    }
}