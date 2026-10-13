// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Services.Managements.SqlValues;
using sql_storage_engine.Services.Managements.SqlExpressions;
using sql_storage_engine.Services.Managements.CatalogIndexKeys;
using sql_storage_engine.Catalog;
using System.Buffers.Binary;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Services.Aggregations.SqlValues;
internal sealed partial class SqlValueRequestAggregationService : ISqlValueRequestAggregationService
{
    private readonly SqlValueRequestManagementService values;
    private readonly SqlExpressionContextManagementService expressions;
    private readonly CatalogIndexKeyRequestManagementService keys;
    private SqlValueRequestAggregationService(SqlValueRequestManagementService values, SqlExpressionContextManagementService expressions, CatalogIndexKeyRequestManagementService keys)
    {
        this.values = values;
        this.expressions = expressions;
        this.keys = keys;
    }

    internal static SqlValueRequestAggregationService CreateSqlValueRequestAggregationService() =>
        new(values: SqlValueRequestManagementService.CreateSqlValueRequestManagementService(), expressions: SqlExpressionContextManagementService.CreateSqlExpressionContextManagementService(), keys: CatalogIndexKeyRequestManagementService.CreateCatalogIndexKeyRequestManagementService());

    public SqlValueRequest CreateSqlValueRequest(SqlValueRequest sqlValueRequest) =>
        TryCatch(operation: () =>
    {
        ValidateCreation(sqlValueRequest: sqlValueRequest);

        return sqlValueRequest.Operation switch
        {
            "Table" => sqlValueRequest with
            {
                Result = CreateTableSqlValue(definition: (TableDefinition)sqlValueRequest.Input!, rows: (IEnumerable<Row>)sqlValueRequest.Other!)
            },
            "CatalogTable" => sqlValueRequest with
            {
                Result = CreateTableSqlValue(definition: (CatalogTableType)sqlValueRequest.Input!, rows: (IEnumerable<Row>)sqlValueRequest.Other!)
            },
            _ => values.CreateSqlValueRequest(sqlValueRequest: sqlValueRequest)};
    });

    public SqlValueRequest CompareSqlValueRequest(SqlValueRequest sqlValueRequest) =>
        TryCatch(operation: () =>
    {
        ValidateComparison(sqlValueRequest: sqlValueRequest);
        return values.CompareSqlValueRequest(sqlValueRequest: sqlValueRequest);
    });

    public SqlValueRequest ReadBinarySqlValueRequest(SqlValueRequest sqlValueRequest) =>
        TryCatch(operation: () =>
    {
        ValidateBinaryRead(sqlValueRequest: sqlValueRequest);
        return values.ReadBinarySqlValueRequest(sqlValueRequest: sqlValueRequest);
    });

    public SqlValueRequest ReadVectorSqlValueRequest(SqlValueRequest sqlValueRequest) =>
        TryCatch(operation: () =>
    {
        ValidateVectorRead(sqlValueRequest: sqlValueRequest);
        return values.ReadVectorSqlValueRequest(sqlValueRequest: sqlValueRequest);
    });

    public SqlValueRequest MeasureVectorSqlValueRequest(SqlValueRequest sqlValueRequest) =>
        TryCatch(operation: () =>
    {
        ValidateVectorDistance(sqlValueRequest: sqlValueRequest);
        return values.MeasureVectorSqlValueRequest(sqlValueRequest: sqlValueRequest);
    });

    private SqlValue CreateTableSqlValue(TableDefinition definition, IEnumerable<Row> rows)
    {
        ArgumentNullException.ThrowIfNull(argument: definition);
        ArgumentNullException.ThrowIfNull(argument: rows);
        var snapshot = rows.ToArray();

        foreach (var row in snapshot)
        {
            values.ValidateSqlValueRequestRow(sqlValueRequest: new SqlValueRequest { Input = definition, Other = row });
        }

        return new TableSqlValue
        {
            Definition = definition,
            Rows = Array.AsReadOnly(array: snapshot)
        };
    }

    private SqlValue CreateTableSqlValue(Models.Catalog.CatalogTableType definition, IEnumerable<Row> rows)
    {
        ArgumentNullException.ThrowIfNull(argument: definition);
        ArgumentNullException.ThrowIfNull(argument: rows);

        var tableDefinition = values.PrepareSqlValueRequestDefinition(sqlValueRequest: new SqlValueRequest { Input = definition.Columns.Select(selector: column => new ColumnDefinition { Id = column.Id, Name = column.Name, Type = column.Type, IsNullable = column.IsNullable, Encryption = column.Encryption })
            .ToArray() })
            .DefinitionResult!;

        var keyTable = new CatalogTable
        {
            Id = new TableId
            {
                Value = 1
            },
            Name = "table_value",
            SchemaVersion = 1,
            FirstHeapPageId = new PageId
            {
                Value = 1
            },
            Columns = definition.Columns,
            DatabaseName = "default",
            SchemaName = "dbo",
            CheckConstraints = []
        };

        var tableIndexes = definition.Indexes.Select(selector: (index, position) => new TableValueIndex { Definition = index, CatalogIndex = new CatalogIndex { Id = new IndexId { Value = checked((ulong)position + 1) }, Name = index.Name, TableId = keyTable.Id, RootPageId = new PageId { Value = 1 }, IsUnique = true, Method = CatalogIndexMethod.BTree, Columns = index.Columns, IncludedColumns = [], StorageKind = CatalogIndexStorageKind.NonClustered }, Keys = [] })
            .ToArray();

        BigInteger? identity = definition.Columns.SingleOrDefault(predicate: column => column.Identity is not null)?.Identity?.Seed;
        ulong rowVersion = 1;
        Span<byte> rowVersionBytes = stackalloc byte[8];
        var normalized = new List<Row>();

        foreach (var row in rows)
        {
            if (row.Values.Count != definition.Columns.Count)
            {
                throw new ArgumentException(message: "Row width does not match table type.", paramName: nameof(rows));
            }

            var values = row.Values.ToArray();
            var context = new Dictionary<string, SqlValue>(comparer: StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < definition.Columns.Count; index++)
            {
                var column = definition.Columns[index];
                var value = values[index];

                if (column.Identity is not null)
                {
                    if (value is not DefaultSqlValue && !value.IsNull)
                    {
                        throw new ArgumentException(message: "Table-type IDENTITY cannot be assigned.", paramName: nameof(rows));
                    }

                    value = Convert(sqlType: column.Type, sqlValue: this.values.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "DecimalParts", Input = identity!.Value, Other = (byte)0 })
            .Result!);

                    identity += column.Identity.Increment;
                }
                else if (column.Type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp)
                {
                    if (value is not DefaultSqlValue && !value.IsNull)
                    {
                        throw new ArgumentException(message: "Table-type rowversion cannot be assigned explicitly.", paramName: nameof(rows));
                    }

                    BinaryPrimitives.WriteUInt64BigEndian(destination: rowVersionBytes, value: rowVersion++);

                    value = this.values.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Binary", Input = rowVersionBytes.ToArray() })
            .Result!;
                }
                else if (column.IsComputed)
                {
                    value = this.values.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Default" })
            .Result!;
                }
                else if (value is DefaultSqlValue)
                {
                    value = column.DefaultExpression is null ? this.values.CreateSqlValueRequest(sqlValueRequest: new SqlValueRequest { Operation = "Null" })
            .Result! : Evaluate(expression: column.DefaultExpression, columns: context);
                }

                if (value is not DefaultSqlValue && !value.IsNull)
                {
                    value = Convert(sqlType: column.Type, sqlValue: value);
                }

                values[index] = value;

                if (value is not DefaultSqlValue)
                {
                    context[column.Name] = value;
                }
            }

            foreach (var index in expressions.ReadSqlExpressionContextComputedOrder(sqlExpressionContext: new SqlExpressionContext { ColumnDefinitions = definition.Columns })
            .ComputedColumnOrder!)
            {
                var column = definition.Columns[index];
                var value = Evaluate(expression: column.ComputedExpression!, columns: context);

                var source = expressions.ReadSqlExpressionContextColumnReference(sqlExpressionContext: new SqlExpressionContext { Source = column.ComputedExpression!, ColumnDefinitions = definition.Columns })
            .DirectColumnReference;

                values[index] = value.IsNull ? value : source is null ? Convert(sqlType: column.Type, sqlValue: value) : Convert(sqlTypes: [column.Type, source.Type], sqlValue: value);
                context[column.Name] = values[index];
            }

            var result = new Row
            {
                Values = values
            };

            this.values.ValidateSqlValueRequestRow(sqlValueRequest: new SqlValueRequest { Input = tableDefinition, Other = result });

            foreach (var check in definition.CheckConstraints)
            {
                var checkedValue = Evaluate(expression: check.Expression, columns: context);

                if (!checkedValue.IsNull && !((BooleanSqlValue)Convert(sqlType: new SqlType { Name = SqlTypeName.Bit }, sqlValue: checkedValue)).Value)
                {
                    throw new ArgumentException(message: $"CHECK constraint '{check.Name}' rejected a table value row.", paramName: nameof(rows));
                }
            }

            var candidateKeys = tableIndexes.Select(selector: index =>
            {
                var keyValues = index.Definition.Columns.Select(selector: indexed => result.Values[definition.Columns.Select(selector: (column, offset) => (column, offset))
            .Single(predicate: item => item.column.Id == indexed.ColumnId)
            .offset])
            .ToArray();

                var maximum = definition.IsMemoryOptimized ? index.Definition.StorageKind == CatalogIndexStorageKind.Hash ? int.MaxValue : 2_500 : index.Definition.StorageKind == CatalogIndexStorageKind.Clustered ? 900 : 1700;

                return (Index: index, Key: keys.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 14, Values = keyValues, Table = keyTable, Index = index.CatalogIndex, MaximumKeyBytes = maximum })
            .Key!);
            })
            .ToArray();

            var hardDuplicate = candidateKeys.FirstOrDefault(predicate: candidate => candidate.Index.Definition.IsUnique && candidate.Index.Keys.Contains(item: candidate.Key) && !candidate.Index.Definition.IgnoreDuplicateKey);

            if (hardDuplicate.Index is not null)
            {
                throw new ArgumentException(message: $"Unique table-type index '{hardDuplicate.Index.Definition.Name}' rejected a duplicate row.", paramName: nameof(rows));
            }

            if (candidateKeys.Any(predicate: candidate => candidate.Index.Definition.IsUnique && candidate.Index.Keys.Contains(item: candidate.Key)))
            {
                continue;
            }

            foreach (var candidate in candidateKeys.Where(predicate: candidate => candidate.Index.Definition.IsUnique))
            {
                candidate.Index.Keys.Add(item: candidate.Key);
            }

            normalized.Add(item: result);
        }

        return new TableSqlValue
        {
            Definition = tableDefinition,
            Rows = normalized.AsReadOnly()
        };
    }

    private SqlValue Convert(SqlType sqlType, SqlValue sqlValue) =>
        values.ConvertSqlValueRequest(sqlValueRequest: new SqlValueRequest { Input = sqlType, Other = sqlValue })
            .Result!;

    private SqlValue Convert(SqlType[] sqlTypes, SqlValue sqlValue) =>
        values.ConvertSqlValueRequest(sqlValueRequest: new SqlValueRequest { Input = sqlTypes[0], Other = sqlValue, Third = sqlTypes[1] })
            .Result!;

    private SqlValue Evaluate(string expression, IReadOnlyDictionary<string, SqlValue> columns) =>
        expressions.EvaluateSqlExpressionContext(sqlExpressionContext: new SqlExpressionContext { Source = expression, Columns = columns })
            .Result!;
}