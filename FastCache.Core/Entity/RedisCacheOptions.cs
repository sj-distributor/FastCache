using System;
using StackExchange.Redis;

namespace FastCache.Core.Entity
{
    public class RedisCacheOptions
    {
        /// <summary>
        /// Quorum 重试次数（默认: 3）
        /// </summary>
        public int QuorumRetryCount { get; set; } = 3;

        /// <summary>
        /// Quorum 重试延迟基准值（默认: 400ms）
        /// </summary>
        public int QuorumRetryDelayMs { get; set; } = 400;

        /// <summary>
        /// 全局延迟删除时间(ms)
        /// </summary>
        public int DoubleDeleteDelayedMs { get; set; } = 2000;

        public Action<object?, ConnectionFailedEventArgs>? ConnectionFailureHandler { get; set; } = null;

        public Action<object?, ConnectionFailedEventArgs>? ConnectionRestoredHandler { get; set; } = null;
    }
}