using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace UnitTests;

public class RedisKeyPatternGeneratorUtil
{
    private readonly Random _rnd = new Random();
    private static readonly Regex CharSetRegex = new Regex(@"\[([^\]]+)\]");
    private static readonly Regex RangeRegex = new Regex(@"\[(\d+)-(\d+)\]");

    public string GenerateRedisCompatibleKey(string pattern, int index)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return $"key_{index}_{Guid.NewGuid().ToString("N")[..8]}";

        var sb = new StringBuilder();
        bool isEscape = false;
        bool inBrackets = false;
        string bracketContent = string.Empty;

        for (int i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];

            if (isEscape)
            {
                sb.Append(c);
                isEscape = false;
                continue;
            }

            switch (c)
            {
                case '\\':
                    isEscape = true;
                    break;

                case '*':
                    sb.Append(inBrackets ? c.ToString() : GenerateWildcardReplacement(index));
                    break;

                case '?':
                    sb.Append(inBrackets ? c.ToString() : _rnd.Next(0, 10).ToString());
                    break;

                case '[':
                    if (!inBrackets)
                    {
                        inBrackets = true;
                        bracketContent = string.Empty;
                    }
                    else
                    {
                        bracketContent += c;
                    }

                    break;

                case ']':
                    if (inBrackets)
                    {
                        var result = ProcessBracketContent(bracketContent);
                        sb.Append(result);
                        inBrackets = false;
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;

                default:
                    if (inBrackets)
                        bracketContent += c;
                    else
                        sb.Append(c);
                    break;
            }
        }

        // 处理未闭合的方括号
        if (inBrackets)
            sb.Append('[').Append(bracketContent);

        // 确保键名符合Redis规范
        return NormalizeRedisKey(sb.ToString());
    }

    private string ProcessBracketContent(string content)
    {
        // 空内容 []
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        // 中文字符集特殊处理（如[张三李四]）
        if (ContainsChineseCharacters(content))
        {
            // 直接返回原内容（去掉方括号）
            return content;
        }

        // 范围模式 [1-9], [a-z]
        if (content.Length == 3 && content[1] == '-')
        {
            char start = content[0];
            char end = content[2];

            if (char.IsDigit(start) && char.IsDigit(end))
                return _rnd.Next(start - '0', end - '0' + 1).ToString();

            if (char.IsLetter(start) && char.IsLetter(end))
                return ((char)_rnd.Next(start, end + 1)).ToString();
        }

        // 普通字符集 [abc]
        if (content.All(char.IsLetterOrDigit) && content.Length > 1)
            return content[_rnd.Next(0, content.Length)].ToString();

        // 默认情况：保留原始内容（去掉方括号）
        return EscapeRedisSpecialChars(content);
    }

    private bool ContainsChineseCharacters(string input)
    {
        foreach (char c in input)
        {
            // Unicode中文字符范围：0x4E00-0x9FFF
            if (c >= '\u4e00' && c <= '\u9fff')
                return true;

            // 扩展判断：全角符号等
            if (c >= '\u3000' && c <= '\u303f')
                return true;
        }

        return false;
    }

    private string EscapeRedisSpecialChars(string input)
    {
        // Redis特殊字符: * ? [ ] 
        var sb = new StringBuilder();
        foreach (char c in input)
        {
            if ("*?[]".Contains(c))
                sb.Append('\\').Append(c);
            else
                sb.Append(c);
        }

        return sb.ToString();
    }

    private string NormalizeRedisKey(string key)
    {
        // 替换空格为下划线
        key = key.Replace(' ', '_');

        // 限制长度
        const int maxLength = 256;
        if (key.Length > maxLength)
            key = key.Substring(0, maxLength);

        return key;
    }

    private string GenerateWildcardReplacement(int index)
    {
        // 使用线程安全的随机数生成
        var rndValue = ThreadLocalRandom.Next(0, 100);

        // 根据不同的权重选择生成策略
        switch (rndValue % 7) // 7种组合方式减少重复
        {
            case 0:
                return $"{index}_{DateTime.UtcNow:HHmmss}";

            case 1:
                return $"{index:X4}{ThreadLocalRandom.Next(100):D2}";

            case 2:
                return $"{DateTime.UtcNow.Ticks % 1000000}_{index}";

            case 3:
                return $"{(char)('A' + index % 26)}{ThreadLocalRandom.Next(1000, 9999)}";

            case 4:
                return $"{Guid.NewGuid():N}".Substring(0, 8 + index % 5);

            case 5:
                return Convert.ToBase64String(BitConverter.GetBytes(DateTime.UtcNow.Ticks))
                    .Replace("=", "")[..8];

            default:
                return $"{index}{DateTime.UtcNow:MMdd}{ThreadLocalRandom.Next(100):D2}";
        }
    }

    // 线程安全的随机数生成器
    private static class ThreadLocalRandom
    {
        private static readonly ThreadLocal<Random> Random = new(() => new Random(Interlocked.Increment(ref _seed)));

        private static int _seed = Environment.TickCount;

        public static int Next(int min, int max) => Random.Value!.Next(min, max);
        public static int Next(int max) => Random.Value!.Next(max);
    }
}