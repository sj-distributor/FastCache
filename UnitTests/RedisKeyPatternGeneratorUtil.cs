using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace UnitTests;

public class RedisKeyPatternGeneratorUtil
{
    private readonly Random _rnd = new Random();
    private static readonly Regex CharSetRegex = new Regex(@"\[([^\]]+)\]");
    private static readonly Regex RangeRegex = new Regex(@"\[(\d+)-(\d+)\]");

    public string GenerateMatchingKey(string pattern, int index)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return $"key_{index}_{Guid.NewGuid().ToString("N")[..8]}";

        // 处理顺序匹配部分
        var sb = new StringBuilder();
        bool inCharSet = false;
        bool inRange = false;
        bool isEscape = false;

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
                    if (!inCharSet && !inRange)
                        sb.Append(GenerateWildcardReplacement(index));
                    else
                        sb.Append(c);
                    break;

                case '?':
                    if (!inCharSet && !inRange)
                        sb.Append(_rnd.Next(0, 10)); // 0-9随机数字
                    else
                        sb.Append(c);
                    break;

                case '[':
                    if (!inCharSet && !inRange)
                    {
                        // 处理字符集 [abc] 或范围 [1-10]
                        var match = RangeRegex.Match(pattern, i);
                        if (match.Success)
                        {
                            var start = int.Parse(match.Groups[1].Value);
                            var end = int.Parse(match.Groups[2].Value);
                            sb.Append(_rnd.Next(start, end + 1));
                            i = match.Index + match.Length - 1;
                            break;
                        }

                        inCharSet = true;
                        sb.Append('[');
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;

                case ']':
                    if (inCharSet || inRange)
                    {
                        inCharSet = false;
                        inRange = false;
                        sb.Append(']');
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;

                default:
                    if (inCharSet && i + 1 < pattern.Length && pattern[i + 1] == '-')
                    {
                        // 处理字符范围 a-z
                        var start = c;
                        var end = pattern[i + 2];
                        sb.Append((char)_rnd.Next(start, end + 1));
                        i += 2;
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        // 如果没有模式字符，添加随机后缀确保唯一性
        if (pattern.IndexOfAny(new[] { '*', '?', '[', ']' }) == -1)
        {
            sb.Append('_').Append(Guid.NewGuid().ToString("N")[..6]);
        }

        return sb.ToString();
    }

    private string GenerateWildcardReplacement(int index)
    {
        // 更智能的通配符替换逻辑
        var options = new[]
        {
            $"{index}",
            $"{DateTime.UtcNow:yyyyMMdd}",
            $"{Guid.NewGuid():N}",
            $"{_rnd.Next(1000, 9999)}"
        };

        return options[_rnd.Next(options.Length)];
    }

    public IEnumerable<string> GenerateKeys(string pattern, int count)
    {
        var generated = new HashSet<string>();
        while (generated.Count < count)
        {
            var key = GenerateMatchingKey(pattern, generated.Count + 1);
            if (generated.Add(key))
                yield return key;
        }
    }
}