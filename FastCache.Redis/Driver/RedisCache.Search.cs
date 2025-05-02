using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FastCache.Core.Entity;

namespace FastCache.Redis.Driver
{
    public partial class RedisCache
    {
        /// <summary>
        /// 高级模糊搜索（支持集群模式和分页控制）
        /// </summary>
        /// <returns></returns>
        public async Task<List<string>> FuzzySearchAsync(
            AdvancedSearchModel advancedSearchModel,
            CancellationToken cancellationToken = default)
        {
            if (advancedSearchModel == null || string.IsNullOrWhiteSpace(advancedSearchModel.Pattern))
                throw new ArgumentException();

            // TODO 集群模式处理

            var result = new List<string>();

            // 单节点模式
            await foreach (var key in NativeScanAsync(advancedSearchModel, cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    result.Add(key);
                }
            }

            return result;
        }

        private static bool CheckLimitReached(AdvancedSearchModel model, int currentCount) =>
            model.MaxResults > 0 && currentCount >= model.MaxResults;

        private async IAsyncEnumerable<string> NativeScanAsync(
            AdvancedSearchModel model,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // 单节点扫描
            var server = _redisConnection.GetServer(_redisConnection.GetEndPoints()[0]);

            var batchBuffer = new List<string>(capacity: model.PageSize);

            foreach (var redisKey in server.Keys(
                         pattern: model.Pattern,
                         pageSize: model.PageSize))
            {
                batchBuffer.Add(redisKey);

                if (cancellationToken.IsCancellationRequested) break;

                foreach (var item in batchBuffer)
                    yield return item;

                batchBuffer.Clear();
                await Task.Delay(1, cancellationToken); // 可控延迟
            }
        }
    }
}