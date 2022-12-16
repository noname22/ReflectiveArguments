using System.Linq;

namespace ReflectiveArguments;

static class StringExtensions
{
    public static string ToKebabCase(this string Name) => string
        .Concat((Name ?? string.Empty)
            .Select((x, i) => i > 0 && char.IsUpper(x) && !char.IsUpper(Name[i - 1]) ? $"-{x}" : x.ToString()))
        .ToLower();
}
