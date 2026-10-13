// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Services.Processings.TableDefinitions;
internal sealed partial class TableDefinitionProcessingService
{
    private static void ValidateTableDefinitionSource(TableDefinition tableDefinition)
    {
        ArgumentNullException.ThrowIfNull(argument: tableDefinition);
        ArgumentNullException.ThrowIfNull(argument: tableDefinition.ColumnSource ?? tableDefinition.Columns);
    }

    private static void ValidateTableDefinition(TableDefinition tableDefinition)
    {
        ArgumentNullException.ThrowIfNull(argument: tableDefinition);
        ArgumentNullException.ThrowIfNull(argument: tableDefinition.Columns);

        if (tableDefinition.Columns.Count == 0)
        {
            throw new ArgumentException(message: "A table must define at least one column.", paramName: "columns");
        }

        foreach (var column in tableDefinition.Columns)
        {
            ValidateColumn(column: column);
        }

        if (tableDefinition.Columns.Select(selector: column => column.Id)
            .Distinct()
            .Count() != tableDefinition.Columns.Count)
        {
            throw new ArgumentException(message: "Column IDs must be unique.", paramName: "columns");
        }

        if (tableDefinition.Columns.Select(selector: column => column.Name)
            .Distinct(comparer: StringComparer.Ordinal)
            .Count() != tableDefinition.Columns.Count)
        {
            throw new ArgumentException(message: "Column names must be ordinally unique.", paramName: "columns");
        }
    }

    private static void ValidateTableDefinitionRow(TableDefinition tableDefinition)
    {
        ArgumentNullException.ThrowIfNull(argument: tableDefinition);
        ArgumentNullException.ThrowIfNull(argument: tableDefinition.SchemaSource, paramName: "tableDefinition");
        ValidateTableDefinition(tableDefinition: tableDefinition.SchemaSource);
        ArgumentNullException.ThrowIfNull(argument: tableDefinition.RowInput, paramName: "row");
        ArgumentNullException.ThrowIfNull(argument: tableDefinition.RowInput.Values);
    }

    private static void ValidateColumn(ColumnDefinition column)
    {
        ArgumentNullException.ThrowIfNull(argument: column);
        var name = column.Name;
        var type = column.Type;
        var isSparse = column.IsSparse;
        var isNullable = column.IsNullable;
        var isColumnSet = column.IsColumnSet;

        if (string.IsNullOrWhiteSpace(value: name))
        {
            throw new ArgumentException("Column name cannot be empty.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(argument: type);

        if (type.Name is SqlTypeName.Cursor or SqlTypeName.Table)
        {
            throw new ArgumentException($"{type.Name} cannot be used as a table column.", nameof(type));
        }

        if (isSparse && !isNullable)
        {
            throw new ArgumentException("SPARSE columns must be nullable.", nameof(isSparse));
        }

        if (isColumnSet && (!isNullable || type.Name != SqlTypeName.Xml))
        {
            throw new ArgumentException("A column set must be nullable XML.", nameof(isColumnSet));
        }
    }
}