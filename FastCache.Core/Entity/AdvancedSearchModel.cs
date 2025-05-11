namespace FastCache.Core.Entity
{
    public class AdvancedSearchModel
    {
        /// <summary>匹配模式（支持*和?）</summary>
        public string Pattern { get; set; } = "*";

        /// <summary>每批次扫描数量</summary>
        public int PageSize { get; set; } = 200;

        /// <summary>最大返回结果数（0表示无限制）</summary>
        public int MaxResults { get; set; } = 1000;

        /// <summary>是否包含值内容（启用时会额外执行GET操作）</summary>
        public bool IncludeValues { get; set; }

        /// <summary>结果过滤条件（Lua脚本片段）</summary>
        public string? FilterScript { get; set; }
    }
}