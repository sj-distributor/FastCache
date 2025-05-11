using System;

namespace FastCache.Core.Entity
{
    public class DistributedLockOptions
    {
        /// <summary>
        /// 锁的自动释放时间（默认30秒）
        /// </summary>
        public TimeSpan ExpiryTime { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 最大等待获取锁时间（默认10秒）
        /// </summary>
        public TimeSpan WaitTime { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 重试间隔时间（默认200毫秒）
        /// </summary>
        public TimeSpan RetryInterval { get; set; } = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// 获取锁失败时是否抛出异常（默认true）
        /// </summary>
        public bool ThrowOnLockFailure { get; set; } = true;

        /// <summary>
        /// 业务操作失败时是否抛出异常（默认false）
        /// </summary>
        public bool ThrowOnOperationFailure { get; set; } = false;

        // 可扩展的Builder模式
        public DistributedLockOptions WithExpiry(TimeSpan expiry)
        {
            ExpiryTime = expiry;
            return this;
        }
    }
}