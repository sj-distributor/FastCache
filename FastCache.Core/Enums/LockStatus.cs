namespace FastCache.Core.Enums
{
    public enum LockStatus
    {
        AcquiredAndCompleted, // 成功获取并执行
        LockNotAcquired, // 锁获取失败
        OperationFailed // 获取锁后执行失败
    }
}