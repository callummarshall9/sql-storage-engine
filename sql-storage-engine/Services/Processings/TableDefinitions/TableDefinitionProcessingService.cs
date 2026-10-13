// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.TableDefinitions;
internal sealed partial class TableDefinitionProcessingService : ITableDefinitionProcessingService
{
    public TableDefinition PrepareTableDefinition(TableDefinition tableDefinition) =>
        TryCatch(operation: () =>
    {
        ValidateTableDefinitionSource(tableDefinition: tableDefinition);
        var columns = (tableDefinition.ColumnSource ?? tableDefinition.Columns).ToArray();

        var prepared = new TableDefinition
        {
            Columns = Array.AsReadOnly(array: columns)
        };

        ValidateTableDefinition(tableDefinition: prepared);
        return prepared;
    });

    public void CheckTableDefinitionRow(TableDefinition tableDefinition) =>
        TryCatch(operation: () =>
    {
        ValidateTableDefinitionRow(tableDefinition: tableDefinition);
        var row = tableDefinition.RowInput!;
        var schema = tableDefinition.SchemaSource!;

        if (row.Values.Count != schema.Columns.Count)
        {
            throw new ArgumentException(message: $"Expected {schema.Columns.Count} values, received {row.Values.Count}.", paramName: nameof(row));
        }

        for (var index = 0; index < schema.Columns.Count; index++)
        {
            ValidateColumnValue(column: schema.Columns[index], value: row.Values[index]);
        }
    });

    private static void ValidateColumnValue(ColumnDefinition column, SqlValue value)
    {
        ArgumentNullException.ThrowIfNull(argument: value);

        if (value is NullSqlValue)
        {
            if (!column.IsNullable)
            {
                throw new ArgumentException(message: $"Column '{column.Name}' is not nullable.", paramName: nameof(value));
            }

            return;
        }

        if (column.IsColumnSet)
        {
            throw new ArgumentException(message: $"Column set '{column.Name}' is a virtual projection.", paramName: nameof(value));
        }
    }
}