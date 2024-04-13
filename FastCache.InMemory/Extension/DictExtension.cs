using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace FastCache.InMemory.Extension
{
    public static class DictExtension
    {
        public static bool TryRemove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key,
            out TValue value, int seconds = 3)
        {
            var res = dictionary.TryRemove(key, out value);
            if (res && seconds > 0)
            {
                Task.Delay(TimeSpan.FromSeconds(seconds)).ContinueWith(_ => { dictionary.TryRemove(key, out var _); });
            }
            return res;
        }
    }
}