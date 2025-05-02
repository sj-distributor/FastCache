using System;
using FastCache.Core.Enums;

namespace FastCache.Core.Entity
{
    public class DistributedLockResult
    {
        public bool IsSuccess { get; set; }
        public LockStatus Status { get; set; }
        public Exception? Exception { get; set; }
    }
}