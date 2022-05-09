namespace EasyCache.Core.Entity
{
    public class CacheItem
    {
        public object? Value { get; set; }

        public string? Type { get; set; }

        public string? AssemblyName { get; set; }
        public long CreatedAt { get; set; }
        public long Expire { get; set; }
        
        public ulong Hits = 0;
    }
}