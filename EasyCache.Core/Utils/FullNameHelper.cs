using System;

namespace EasyCache.Core.Utils
{
    public static class FullNameHelper
    {
        public static string GetFullName(this Type type)
        {
            var str = string.Empty;
            if (type.IsGenericType)
            {
                //获取泛型定义
                //FullName:System.Collections.Generic.List`1
                str = type.GetGenericTypeDefinition().FullName;
                //获取所有泛型类型的数组
                var args = type.GetGenericArguments();
                //泛型类型的数组索引
                int argIndex = 0;
                while (true)
                {
                    var startIndex = str.IndexOf('`');
                    if (startIndex < 0) break;
                    //获取外部类的泛型数量
                    var argNum = System.Convert.ToInt32(str.Substring(startIndex + 1, 1));
                    string tmp = string.Empty;
                    tmp += "<";
                    for (int i = 0; i < argNum; i++, argIndex++)
                    {
                        if (i < argNum - 1)
                            tmp += $"{args[argIndex].GetFullName()},";
                        else
                            tmp += $"{args[argIndex].GetFullName()}";
                    }

                    tmp += ">";
                    str = str.Remove(startIndex, 2);
                    str = str.Insert(startIndex, tmp);
                }
            }
            else
            {
                str = type.FullName ?? string.Empty;
            }

            if (type.IsNested)
            {
                //替换嵌套类的+
                str = str.Replace('+', '.');
            }

            return str;
        }
    }
}