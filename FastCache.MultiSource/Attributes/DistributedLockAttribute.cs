using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AspectCore.DynamicProxy;
using FastCache.Core.Driver;
using Microsoft.Extensions.DependencyInjection;

namespace FastCache.MultiSource.Attributes
{
    public class DistributedLockAttribute : AbstractInterceptorAttribute
    {
        private readonly string _prefix;
        private readonly int _msTimeout;
        private readonly int _msExpire;
        private readonly bool _throwOnFailure;
        private readonly bool _usePrefixToKey;

        public DistributedLockAttribute(string prefix,
            int msTimeout = 600,
            int msExpire = 3000,
            bool throwOnFailure = false,
            bool usePrefixToKey = true)
        {
            _prefix = prefix;
            _msTimeout = msTimeout;
            _msExpire = msExpire;
            _throwOnFailure = throwOnFailure;
            _usePrefixToKey = usePrefixToKey;
        }

        public override async Task Invoke(AspectContext context, AspectDelegate next)
        {
            var cacheClient = context.ServiceProvider.GetService<IRedisCache>();

            // 根据方法名和参数生成唯一的锁定键
            var lockKey = _usePrefixToKey ? $"{_prefix}:{context.ServiceMethod.Name}" : context.ServiceMethod.Name;
            GenerateLockKey(context);

            await cacheClient.ExecuteWithRedisLockAsync(
                lockKey, async () => { await next(context); }, _msTimeout, _msExpire, _throwOnFailure);
        }

        private string GenerateLockKey(AspectContext context)
        {
            // 获取方法名
            var methodName = context.ServiceMethod.Name;

            // 获取方法参数并转换成字符串表示
            var arguments = context.Parameters.Select(p => p?.ToString() ?? "null").ToArray();

            // 拼接方法名和参数作为输入
            var combined = $"{methodName}:{string.Join(":", arguments)}";

            // 对拼接后的字符串进行哈希处理，得到简短唯一的Key
            var hash = ComputeSha256Hash(combined);

            // 生成Key：前缀 + 哈希值
            var key = $"{_prefix}:{hash}";

            return key;
        }

        // 使用SHA-256哈希算法来确保唯一性并缩短Key长度
        private string ComputeSha256Hash(string rawData)
        {
            using var sha256 = SHA256.Create();
            // 计算哈希值
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            // 将字节数组转换为十六进制字符串，并取前8个字节，确保短且唯一
            return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16);
        }
    }
}