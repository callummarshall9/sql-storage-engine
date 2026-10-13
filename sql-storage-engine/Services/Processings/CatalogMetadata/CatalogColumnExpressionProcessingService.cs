// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogColumnExpressionProcessingService : ICatalogColumnExpressionProcessingService
{
    public CatalogColumnExpressionContext PrepareCatalogColumnExpressionContext(CatalogColumnExpressionContext catalogColumnExpressionContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogColumnExpressionContext(catalogColumnExpressionContext: catalogColumnExpressionContext);

        switch (catalogColumnExpressionContext.Operation)
        {
            case 1:
                catalogColumnExpressionContext.ReferencesColumn = ExpressionReferencesColumn(expression: catalogColumnExpressionContext.Expression!, columnName: catalogColumnExpressionContext.ColumnName!);
                break;
            case 2:
                catalogColumnExpressionContext.ComputedColumnOrder = GetComputedColumnOrder(columns: catalogColumnExpressionContext.Columns!);
                break;
            case 3:
                catalogColumnExpressionContext.DirectColumnReference = ResolveDirectColumnReference(expression: catalogColumnExpressionContext.Expression!, columns: catalogColumnExpressionContext.Columns!);
                break;
        }

        return catalogColumnExpressionContext;
    });

    private static bool ExpressionReferencesColumn(string expression, string columnName)
    {
        ArgumentNullException.ThrowIfNull(argument: expression);
        ArgumentNullException.ThrowIfNull(argument: columnName);

        foreach (var identifier in ExpressionIdentifiers(expression: expression))
        {
            if (identifier.Equals(value: columnName, comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<int> GetComputedColumnOrder(IReadOnlyList<CatalogColumn> columns)
    {
        var computed = columns.Select(selector: (column, position) => (column, position))
            .Where(predicate: item => item.column.IsComputed)
            .ToArray();

        if (computed.Length == 0)
        {
            return[];
        }

        var computedNames = computed.Select(selector: item => item.column.Name)
            .ToHashSet(comparer: StringComparer.OrdinalIgnoreCase);

        var dependencies = computed.ToDictionary(keySelector: item => item.position, elementSelector: item => computedNames.Where(predicate: name => ExpressionReferencesColumn(expression: item.column.ComputedExpression!, columnName: name))
            .ToHashSet(comparer: StringComparer.OrdinalIgnoreCase));

        var completed = new HashSet<string>(comparer: StringComparer.OrdinalIgnoreCase);
        var order = new List<int>(capacity: computed.Length);

        while (order.Count != computed.Length)
        {
            var progressed = false;

            foreach (var item in computed.Where(predicate: item => !order.Contains(item: item.position)))
            {
                if (!dependencies[item.position].IsSubsetOf(other: completed))
                {
                    continue;
                }

                order.Add(item: item.position);
                completed.Add(item: item.column.Name);
                progressed = true;
            }

            if (!progressed)
            {
                throw new ArgumentException(message: "Computed-column dependencies contain a cycle.", paramName: nameof(columns));
            }
        }

        return order;
    }

    private static CatalogColumn? ResolveDirectColumnReference(string expression, IReadOnlyList<CatalogColumn> columns)
    {
        var token = expression.Trim();

        if (token.Length >= 2 && token[0] == '[' && token[^1] == ']')
        {
            token = token[1..^1].Replace(oldValue: "]]", newValue: "]", comparisonType: StringComparison.Ordinal);
        }

        return columns.SingleOrDefault(predicate: column => column.Name.Equals(value: token, comparisonType: StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> ExpressionIdentifiers(string expression)
    {
        for (var position = 0; position < expression.Length;)
        {
            if (expression[position] == '\'')
            {
                position++;

                while (position < expression.Length)
                {
                    if (expression[position++] != '\'')
                    {
                        continue;
                    }

                    if (position < expression.Length && expression[position] == '\'')
                    {
                        position++;
                        continue;
                    }

                    break;
                }

                continue;
            }

            if (expression[position] == '[')
            {
                var identifier = new System.Text.StringBuilder();
                position++;

                while (position < expression.Length)
                {
                    if (expression[position] != ']')
                    {
                        identifier.Append(value: expression[position++]);
                        continue;
                    }

                    position++;

                    if (position < expression.Length && expression[position] == ']')
                    {
                        identifier.Append(value: ']');
                        position++;
                        continue;
                    }

                    break;
                }

                if (identifier.Length != 0)
                {
                    yield return identifier.ToString();
                }

                continue;
            }

            if (!char.IsLetter(c: expression[position]) && expression[position] is not ('_' or '@'))
            {
                position++;
                continue;
            }

            var start = position++;

            while (position < expression.Length && (char.IsLetterOrDigit(c: expression[position]) || expression[position] is '_' or '@' or '$'))
            {
                position++;
            }

            var token = expression[start..position];
            var next = position;

            while (next < expression.Length && char.IsWhiteSpace(c: expression[next]))
            {
                next++;
            }

            if (next < expression.Length && expression[next] == '(' || token.Equals(value: "CURRENT_TIMESTAMP", comparisonType: StringComparison.OrdinalIgnoreCase) || token.Equals(value: "NULL", comparisonType: StringComparison.OrdinalIgnoreCase) || token.Equals(value: "AND", comparisonType: StringComparison.OrdinalIgnoreCase) || token.Equals(value: "OR", comparisonType: StringComparison.OrdinalIgnoreCase) || token.Equals(value: "NOT", comparisonType: StringComparison.OrdinalIgnoreCase) || token.Equals(value: "IS", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return token;
        }
    }
}