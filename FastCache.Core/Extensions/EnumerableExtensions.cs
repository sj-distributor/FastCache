using System;
using System.Collections.Generic;

namespace FastCache.Core.Extensions
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// 将序列按指定大小分块（兼容 netstandard2.1）
        /// </summary>
        /// <typeparam name="T">元素类型</typeparam>
        /// <param name="source">输入序列</param>
        /// <param name="size">每块大小</param>
        /// <returns>分块后的序列</returns>
        public static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> source, int size)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));

            using var enumerator = source.GetEnumerator();
            while (enumerator.MoveNext())
            {
                yield return GetChunk(enumerator, size);
            }
        }

        private static IEnumerable<T> GetChunk<T>(IEnumerator<T> enumerator, int size)
        {
            do
            {
                yield return enumerator.Current;
            } while (--size > 0 && enumerator.MoveNext());
        }
    }
}