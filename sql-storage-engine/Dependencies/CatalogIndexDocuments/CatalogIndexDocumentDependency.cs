// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace sql_storage_engine.Dependencies.CatalogIndexDocuments;
internal sealed class CatalogIndexDocumentDependency : IDisposable
{
    private readonly object sync = new();
    private JsonDocument? document;
    private readonly UTF8Encoding utf8 = new(false, true);
    public (int Kind, string Raw, string? Text)[] ReadPathValues(string json, string path)
    {
        lock (sync)
        {
            document?.Dispose();
            document = JsonDocument.Parse(json: json, options: new JsonDocumentOptions { MaxDepth = 128 });

            return SelectJsonPath(root: document.RootElement, path: path)
            .Select(selector: value => ((int)value.ValueKind, value.GetRawText(), value.ValueKind == JsonValueKind.String ? value.GetString() : null))
            .ToArray();
        }
    }

    public (string Path, int Kind, string Raw, string? Text)[] ReadDescendants(string json, string path, string normalizedPath)
    {
        lock (sync)
        {
            document?.Dispose();
            document = JsonDocument.Parse(json: json, options: new JsonDocumentOptions { MaxDepth = 128 });
            var results = new List<(string Path, int Kind, string Raw, string? Text)>();

            foreach (var item in SelectJsonPath(root: document.RootElement, path: path))
            {
                AddJsonDescendantEntries(value: item, path: normalizedPath, entries: results);
            }

            return results.ToArray();
        }
    }

    public string NormalizePath(string path) =>
        NormalizeJsonPath(path: path);

    public byte[] ReadUtf8Bytes(string value) =>
        utf8.GetBytes(s: value);

    public string ReadUtf8String(byte[] value) =>
        utf8.GetString(bytes: value);

    public void Dispose()
    {
        lock (sync)
        {
            document?.Dispose();
            document = null;
        }
    }

    private static IReadOnlyList<JsonElement> SelectJsonPath(JsonElement root, string path)
    {
        List<JsonElement> values = [root];

        if (path == "$")
        {
            return values;
        }

        var position = 1;

        while (position < path.Length)
        {
            if (path[position] == '.')
            {
                position++;

                if (position < path.Length && path[position] == '*')
                {
                    position++;

                    values = values.Where(predicate: value => value.ValueKind == JsonValueKind.Object)
            .SelectMany(selector: value => value.EnumerateObject()
            .Select(selector: property => property.Value))
            .ToList();

                    continue;
                }

                string property;

                if (position < path.Length && path[position] == '"')
                {
                    var literalStart = position++;

                    while (position < path.Length && path[position] != '"')
                    {
                        if (path[position] == '\\' && position + 1 < path.Length)
                        {
                            position++;
                        }

                        position++;
                    }

                    if (position >= path.Length)
                    {
                        throw new ArgumentException(message: $"JSON path '{path}' has an unclosed quoted property.");
                    }

                    property = JsonSerializer.Deserialize<string>(json: path[literalStart..++position])!;
                }
                else
                {
                    var start = position;

                    while (position < path.Length && path[position] is not ('.' or '['))
                    {
                        position++;
                    }

                    if (start == position)
                    {
                        throw new ArgumentException(message: $"JSON path '{path}' has an empty property.");
                    }

                    property = path[start..position];
                }

                values = values.Where(predicate: value => value.ValueKind == JsonValueKind.Object)
            .SelectMany(selector: value => value.EnumerateObject()
            .Where(predicate: item => item.NameEquals(text: property))
            .Take(count: 1)
            .Select(selector: item => item.Value))
            .ToList();
            }
            else if (path[position] == '[')
            {
                var end = path.IndexOf(value: ']', startIndex: position + 1);

                if (end < 0)
                {
                    throw new ArgumentException(message: $"JSON path '{path}' has an unclosed array selector.");
                }

                var selector = path[(position + 1)..end].Trim();

                values = values.Where(predicate: value => value.ValueKind == JsonValueKind.Array)
            .SelectMany(selector: value => SelectArray(array: value, selector: selector, path: path))
            .ToList();

                position = end + 1;
            }
            else
            {
                throw new ArgumentException(message: $"JSON path '{path}' is malformed.");
            }
        }

        return values;

        static IEnumerable<JsonElement> SelectArray(JsonElement array, string selector, string path)
        {
            if (selector == "*")
            {
                return array.EnumerateArray()
            .ToArray();
            }

            if (selector.Equals(value: "last", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                return array.GetArrayLength() == 0 ? [] : [array[array.GetArrayLength() - 1]];
            }

            var range = selector.Split(separator: " to ", options: StringSplitOptions.TrimEntries);

            if (range.Length == 2 && int.TryParse(s: range[0], result: out var start) && int.TryParse(s: range[1], result: out var finish) && start >= 0 && finish >= start)
            {
                if (start >= array.GetArrayLength())
                {
                    return[];
                }

                return Enumerable.Range(start: start, count: Math.Min(val1: finish, val2: array.GetArrayLength() - 1) - start + 1)
            .Select(selector: index => array[index]);
            }

            if (selector.Contains(value: ','))
            {
                var length = array.GetArrayLength();

                return selector.Split(separator: ',', options: StringSplitOptions.TrimEntries)
            .Select(selector: item => item.Equals(value: "last", comparisonType: StringComparison.OrdinalIgnoreCase) ? length - 1 : int.Parse(s: item, style: System.Globalization.NumberStyles.None, provider: System.Globalization.CultureInfo.InvariantCulture))
            .Where(predicate: index => index >= 0 && index < length)
            .Select(selector: index => array[index])
            .ToArray();
            }

            if (int.TryParse(s: selector, result: out var index) && index >= 0)
            {
                return index < array.GetArrayLength() ? [array[index]] : [];
            }

            throw new ArgumentException(message: $"JSON path '{path}' has an invalid array selector.");
        }
    }

    private static string NormalizeJsonPath(string path)
    {
        var output = new StringBuilder(value: "$");
        var position = 1;

        while (position < path.Length)
        {
            if (path[position] == '.')
            {
                position++;

                if (path[position] == '*')
                {
                    output.Append(value: ".*");
                    position++;
                    continue;
                }

                string property;

                if (path[position] == '"')
                {
                    var literalStart = position++;

                    while (path[position] != '"')
                    {
                        if (path[position++] == '\\')
                        {
                            position++;
                        }
                    }

                    property = JsonSerializer.Deserialize<string>(json: path[literalStart..++position])!;
                }
                else
                {
                    var start = position;

                    while (position < path.Length && path[position] is not ('.' or '['))
                    {
                        position++;
                    }

                    property = path[start..position];
                }

                output.Append(value: '.')
            .Append(value: JsonSerializer.Serialize(value: property));
            }
            else
            {
                var end = path.IndexOf(value: ']', startIndex: position + 1);
                var selector = path[(position + 1)..end].Trim();

                if (selector.Contains(value: ','))
                {
                    selector = string.Join(separator: ", ", values: selector.Split(separator: ',', options: StringSplitOptions.TrimEntries)
            .Select(selector: item => item.Equals(value: "last", comparisonType: StringComparison.OrdinalIgnoreCase) ? "last" : int.Parse(s: item, provider: System.Globalization.CultureInfo.InvariantCulture)
            .ToString(provider: System.Globalization.CultureInfo.InvariantCulture)));
                }
                else if (selector.Contains(value: " to ", comparisonType: StringComparison.Ordinal))
                {
                    var bounds = selector.Split(separator: " to ", options: StringSplitOptions.TrimEntries);

                    selector = int.Parse(s: bounds[0], provider: System.Globalization.CultureInfo.InvariantCulture)
            .ToString(provider: System.Globalization.CultureInfo.InvariantCulture) + " to " + int.Parse(s: bounds[1], provider: System.Globalization.CultureInfo.InvariantCulture)
            .ToString(provider: System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (selector.Equals(value: "last", comparisonType: StringComparison.OrdinalIgnoreCase))
                {
                    selector = "last";
                }
                else if (selector != "*")
                {
                    selector = int.Parse(s: selector, provider: System.Globalization.CultureInfo.InvariantCulture)
            .ToString(provider: System.Globalization.CultureInfo.InvariantCulture);
                }

                output.Append(value: '[')
            .Append(value: selector)
            .Append(value: ']');

                position = end + 1;
            }
        }

        return output.ToString();
    }

    private static void AddJsonDescendantEntries(JsonElement value, string path, ICollection<(string Path, int Kind, string Raw, string? Text)> entries)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(comparer: StringComparer.Ordinal);

            foreach (var property in value.EnumerateObject())
            {
                if (!seen.Add(item: property.Name))
                {
                    continue;
                }

                var childPath = path + "." + JsonSerializer.Serialize(value: property.Name);
                entries.Add(item: Observe(path: childPath, value: property.Value));
                AddJsonDescendantEntries(value: property.Value, path: childPath, entries: entries);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var items = value.EnumerateArray()
            .ToArray();

            for (var position = 0; position < items.Length; position++)
            {
                var aliases = new List<string>
                {
                    path + "[" + position.ToString(provider: System.Globalization.CultureInfo.InvariantCulture) + "]",
                    path + "[*]"
                };

                if (position == items.Length - 1)
                {
                    aliases.Add(item: path + "[last]");
                }

                foreach (var childPath in aliases)
                {
                    entries.Add(item: Observe(path: childPath, value: items[position]));
                    AddJsonDescendantEntries(value: items[position], path: childPath, entries: entries);
                }
            }
        }
    }

    private static (string Path, int Kind, string Raw, string? Text) Observe(string path, JsonElement value) =>
        (path, (int)value.ValueKind, value.GetRawText(), value.ValueKind == JsonValueKind.String ? value.GetString() : null);
}