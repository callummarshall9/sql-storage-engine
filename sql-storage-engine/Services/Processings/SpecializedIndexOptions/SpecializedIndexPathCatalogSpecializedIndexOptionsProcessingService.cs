// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using System.Collections.Generic;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using System.Collections.ObjectModel;
using sql_storage_engine.Services.Foundations.SpecializedIndexPaths;

namespace sql_storage_engine.Services.Processings.SpecializedIndexOptions;
internal sealed partial class SpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService(SpecializedIndexPathService paths) : ISpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService
{
    public CatalogSpecializedIndexOptions PrepareCatalogSpecializedIndexOptions(CatalogSpecializedIndexOptions catalogSpecializedIndexOptions) =>
        TryCatch(operation: () =>
    {
        ValidateOptions(catalogSpecializedIndexOptions: catalogSpecializedIndexOptions);
        var input = catalogSpecializedIndexOptions.PathSource?.ToArray() ?? catalogSpecializedIndexOptions.JsonPaths?.ToArray() ?? [];
        var bindings = new Dictionary<string, string>(StringComparer.Ordinal);

        if (catalogSpecializedIndexOptions.Method == CatalogIndexMethod.Json)
        {
            if (input.Length == 0)
            {
                input = ["$"];
            }

            ValidateJsonPaths(values: input);

            foreach (var path in input)
            {
                ValidateJsonPath(path: path);
            }
        }
        else if (catalogSpecializedIndexOptions.Method == CatalogIndexMethod.Xml)
        {
            ValidateXmlPaths(catalogSpecializedIndexOptions: catalogSpecializedIndexOptions, values: input);

            foreach (var binding in catalogSpecializedIndexOptions.NamespaceSource ?? catalogSpecializedIndexOptions.XmlNamespaces ?? new Dictionary<string, string>())
            {
                ValidateNamespace(prefix: binding.Key, uri: binding.Value);

                try
                {
                    paths.CheckSpecializedIndexPathNamespace(prefix: binding.Key);
                }
                catch (ArgumentException exception)
                {
                    throw new ArgumentException(message: $"XML namespace prefix '{binding.Key}' is invalid.", paramName: "namespaces", innerException: exception);
                }

                bindings.Add(key: binding.Key, value: binding.Value);
            }

            var snapshot = bindings.Select(selector: pair => (pair.Key, pair.Value))
            .ToArray();

            foreach (var path in input.Where(predicate: path => path != "/"))
            {
                try
                {
                    paths.CheckSpecializedIndexPathXPath(path: path, namespaces: snapshot);
                }
                catch (ArgumentException exception)
                {
                    throw new ArgumentException(message: $"XML index path '{path}' is invalid.", paramName: "paths", innerException: exception);
                }
            }

            if (input.Length == 0)
            {
                input = ["/"];
            }
        }

        return catalogSpecializedIndexOptions with
        {
            JsonPaths = Array.AsReadOnly(array: input),
            XmlNamespaces = new ReadOnlyDictionary<string, string>(bindings),
            PathSource = null,
            NamespaceSource = null,
            RequireNamespaces = false,
            RequirePaths = false
        };
    });

    private static bool PathsOverlap(string left, string right) =>
        IsAncestor(prefix: left, value: right) || IsAncestor(prefix: right, value: left);

    private static bool IsAncestor(string prefix, string value) =>
        value.Equals(value: prefix, comparisonType: StringComparison.Ordinal) || value.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal) && value.Length > prefix.Length && value[prefix.Length] is '.' or '[';

    private void ValidateJsonPath(string path)
    {
        var position = 1;
        var depth = 0;

        while (position < path.Length)
        {
            if (++depth > 128)
            {
                throw new ArgumentException($"JSON path '{path}' exceeds 128 levels.", nameof(path));
            }

            if (path[position] == '.')
            {
                position++;

                if (position >= path.Length)
                {
                    throw new ArgumentException($"JSON path '{path}' ends with an empty property.", nameof(path));
                }

                if (path[position] == '*')
                {
                    position++;
                    continue;
                }

                if (path[position] == '"')
                {
                    var literalStart = position++;

                    while (position < path.Length && path[position] != '"')
                    {
                        if (path[position++] == '\\')
                        {
                            if (position >= path.Length)
                            {
                                throw new ArgumentException($"JSON path '{path}' has an incomplete escape.", nameof(path));
                            }

                            position++;
                        }
                    }

                    if (position >= path.Length)
                    {
                        throw new ArgumentException($"JSON path '{path}' has an invalid quoted property.", nameof(path));
                    }

                    try
                    {
                        paths.CheckSpecializedIndexPathJsonProperty(value: path[literalStart..++position]);
                    }
                    catch (ArgumentException exception)
                    {
                        throw new ArgumentException(message: $"JSON path '{path}' has an invalid quoted property.", paramName: nameof(path), innerException: exception);
                    }

                    continue;
                }

                var start = position;

                while (position < path.Length && path[position] is not ('.' or '['))
                {
                    position++;
                }

                if (start == position || path[start..position].Any(predicate: char.IsWhiteSpace))
                {
                    throw new ArgumentException($"JSON path '{path}' has an invalid property.", nameof(path));
                }

                continue;
            }

            if (path[position] == '[')
            {
                var end = path.IndexOf(value: ']', startIndex: position + 1);

                if (end < 0)
                {
                    throw new ArgumentException($"JSON path '{path}' has an unclosed array selector.", nameof(path));
                }

                var selector = path[(position + 1)..end].Trim();
                var valid = selector == "*" || selector.Equals(value: "last", comparisonType: StringComparison.OrdinalIgnoreCase) || int.TryParse(s: selector, result: out var item) && item >= 0;

                if (!valid)
                {
                    var range = selector.Split(separator: " to ", options: StringSplitOptions.TrimEntries);
                    valid = range.Length == 2 && int.TryParse(s: range[0], result: out var start) && int.TryParse(s: range[1], result: out var finish) && start >= 0 && finish >= start;
                }

                if (!valid && selector.Contains(value: ','))
                {
                    var items = selector.Split(separator: ',', options: StringSplitOptions.TrimEntries);

                    valid = items.Length > 1 && items.All(predicate: item => item.Equals(value: "last", comparisonType: StringComparison.OrdinalIgnoreCase) || int.TryParse(s: item, result: out var index) && index >= 0) && items.Distinct(comparer: StringComparer.OrdinalIgnoreCase)
            .Count() == items.Length;
                }

                if (!valid)
                {
                    throw new ArgumentException($"JSON path '{path}' has an invalid array selector.", nameof(path));
                }

                position = end + 1;
                continue;
            }

            throw new ArgumentException($"JSON path '{path}' is malformed.", nameof(path));
        }
    }

    internal static SpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService CreateSpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService() =>
        new SpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService(paths: sql_storage_engine.Services.Foundations.SpecializedIndexPaths.SpecializedIndexPathService.CreateSpecializedIndexPathService());
}