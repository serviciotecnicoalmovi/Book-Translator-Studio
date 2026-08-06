using System.Text.Json;
using System.Text.RegularExpressions;

namespace BookTranslatorStudio.Services;

public static partial class JsonPathReader
{
    public static string ReadString(
        JsonElement root,
        string path)
    {
        var current = root;

        foreach (Match match in PathTokenRegex().Matches(path))
        {
            var property = match.Groups["property"].Value;
            if (!string.IsNullOrEmpty(property))
            {
                current = current.GetProperty(property);
                continue;
            }

            var indexText = match.Groups["index"].Value;
            current = current[int.Parse(indexText)];
        }

        return current.ValueKind == JsonValueKind.String
            ? current.GetString() ?? string.Empty
            : current.ToString();
    }

    [GeneratedRegex(@"(?:(?<property>[^.\[\]]+)|\[(?<index>\d+)\])\.?")]
    private static partial Regex PathTokenRegex();
}
