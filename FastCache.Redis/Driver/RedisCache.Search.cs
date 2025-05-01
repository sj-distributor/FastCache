using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FastCache.Core.Entity;
using StackExchange.Redis;

namespace FastCache.Redis.Driver
{
    public partial class RedisCache
    {
        /// <summary>
        /// 高级模糊搜索（支持集群模式和分页控制）
        /// </summary>
        /// <returns></returns>
        public async IAsyncEnumerable<string> FuzzySearchAsync(
            AdvancedSearchModel advancedSearchModel,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (advancedSearchModel == null || string.IsNullOrWhiteSpace(advancedSearchModel.Pattern))
                throw new ArgumentException();

            // TODO 集群模式处理

            // 单节点模式
            await foreach (var key in NativeScanAsync(advancedSearchModel, cancellationToken))
            {
                yield return key;
            }
        }

        private static bool CheckLimitReached(AdvancedSearchModel model, int currentCount) =>
            model.MaxResults > 0 && currentCount >= model.MaxResults;

        private async IAsyncEnumerable<string> NativeScanAsync(
            AdvancedSearchModel model,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var resultsCount = 0;

            // 集群模式处理
            // TODO

            // 单节点扫描
            var server = _redisConnection.GetServer(_redisConnection.GetEndPoints()[0]);

            foreach (var redisKey in server.Keys(
                         pattern: model.Pattern,
                         pageSize: model.PageSize))
            {
                if (cancellationToken.IsCancellationRequested) break;

                if (string.IsNullOrWhiteSpace(redisKey))
                {
                    if (CheckLimitReached(model, ++resultsCount)) yield break;
                    yield return redisKey;
                }

                // CPU友好设计（每100次迭代释放线程）
                if (resultsCount % 100 == 0)
                    await Task.Yield();
            }
        }
    }
}