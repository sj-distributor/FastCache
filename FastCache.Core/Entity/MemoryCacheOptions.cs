using FastCache.Core.Enums;

namespace FastCache.Core.Entity
{
    public class MemoryCacheOptions
    {
        /// <summary>
        /// 最大缓存项数量（默认：1,000,000）
        /// </summary>
        public int MaxCapacity { get; set; } = 1000000;

        /// <summary>
        /// 内存淘汰策略（默认：LRU - 最近最少使用）
        /// </summary>
        public MaxMemoryPolicy MemoryPolicy { get; set; } = MaxMemoryPolicy.LRU;

        /// <summary>
        /// 内存清理百分比（范围：1-100，默认：10%）
        /// </summary>
        public int CleanUpPercentage { get; set; } = 10;

        /// <summary>
        /// 延迟秒数
        /// </summary>
        public int DelaySeconds { get; set; } = 2;

        public uint Buckets { get; set; } = 5;
    }
}