using System.Collections.Generic;

namespace WebAssemblyInfo
{
    public static class Extensions
    {
        public static string Indent(this string str, string indent)
        {
            return indent + str.Replace("\n", "\n" + indent);
        }

        public static IEnumerable<(T, int)> WithIndex<T>(this IEnumerable<T> source)
        {
            int idx = 0;
            foreach (var item in source)
            {
                yield return (item, idx++);
            }
        }
    }
}
