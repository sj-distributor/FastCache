using System;
using System.Text;

namespace UnitTests;

public partial class RedisCacheTests
{
    public string GenerateDataString(int sizeInKb)
    {
        int sizeInBytes = sizeInKb * 1024;
        var random = new Random();
        var stringBuilder = new StringBuilder(sizeInBytes);

        for (int i = 0; i < sizeInBytes; i++)
        {
            // 随机生成字符，字符范围可以根据需求调整
            stringBuilder.Append((char)random.Next(33, 126)); // 生成ASCII字符从 '!' 到 '~'
        }

        return stringBuilder.ToString();
    }
}