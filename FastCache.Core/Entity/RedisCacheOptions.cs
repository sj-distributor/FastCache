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
    }
}