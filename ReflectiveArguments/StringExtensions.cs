using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReflectiveArguments
{
    static class StringExtensions
    {
        public static string ToKebabCase(this string Name) => string
            .Concat((Name ?? string.Empty)
                .Select((x, i) => i > 0 && char.IsUpper(x) && !char.IsUpper(Name[i - 1]) ? $"-{x}" : x.ToString()))
            .ToLower();

        public static string ToUpperSnakeCase(this string Name) => string
            .Concat((Name ?? string.Empty)
                .Select((x, i) => i > 0 && char.IsUpper(x) && !char.IsUpper(Name[i - 1]) ? $"_{x}" : x.ToString()))
            .ToUpper();
    }
}
