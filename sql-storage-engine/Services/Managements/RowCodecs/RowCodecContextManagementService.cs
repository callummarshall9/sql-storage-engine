// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Coordinations.SqlTypes;
using sql_storage_engine.Services.Coordinations.RowFormats;
using sql_storage_engine.Services.Coordinations.RowStorage;

namespace sql_storage_engine.Services.Managements.RowCodecs;
internal sealed partial class RowCodecContextManagementService : IRowCodecContextManagementService
{
    private readonly SqlTypeContextCoordinationService types;
    private readonly RowFormatContextCoordinationService formats;
    private readonly RowStorageContextCoordinationService storage;
    private readonly RowEncodingConfiguration configuration;
    private readonly bool overflowSourceBound;
    private RowCodecContextManagementService(SqlTypeContextCoordinationService types, RowFormatContextCoordinationService formats, RowStorageContextCoordinationService storage, RowEncodingConfiguration rowEncodingConfiguration)
    {
        this.types = types;
        this.formats = formats;
        this.storage = storage;
        configuration = rowEncodingConfiguration;
        overflowSourceBound = rowEncodingConfiguration.OverflowSourceBound || rowEncodingConfiguration.OverflowService is not null;
    }

    internal static RowCodecContextManagementService CreateRowCodecContextManagementService(RowEncodingConfiguration rowEncodingConfiguration) =>
        new RowCodecContextManagementService(types: SqlTypeContextCoordinationService.CreateSqlTypeContextCoordinationService(), formats: RowFormatContextCoordinationService.CreateRowFormatContextCoordinationService(), storage: RowStorageContextCoordinationService.CreateRowStorageContextCoordinationService(rowEncodingConfiguration: rowEncodingConfiguration), rowEncodingConfiguration: rowEncodingConfiguration);

    public RowEncodingContext ProcessRowEncodingContextAdmission(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateRow(rowEncodingContext: rowEncodingContext);
        PrepareRow(rowEncodingContext: rowEncodingContext);
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextConversion(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateConversion(rowEncodingContext: rowEncodingContext);

        rowEncodingContext.Value = rowEncodingContext.SourceType is null ? types.ProcessSqlTypeContextConvert78(sqlTypeContext: new SqlTypeContext { SqlTypeSource = rowEncodingContext.TargetType, InputSqlValueValue = rowEncodingContext.Value })
            .SqlValueResult : types.ProcessSqlTypeContextConvert79(sqlTypeContext: new SqlTypeContext { SqlTypes = [rowEncodingContext.TargetType!, rowEncodingContext.SourceType], InputSqlValueValue = rowEncodingContext.Value })
            .SqlValueResult;

        return rowEncodingContext;
    });

    internal static RowCodecContextManagementService CreateRowCodecContextManagementService(RowEncodingConfiguration rowEncodingConfiguration, OverflowPageResourceConfiguration overflowPageResourceConfiguration) =>
        new RowCodecContextManagementService(types: SqlTypeContextCoordinationService.CreateSqlTypeContextCoordinationService(), formats: RowFormatContextCoordinationService.CreateRowFormatContextCoordinationService(), storage: RowStorageContextCoordinationService.CreateRowStorageContextCoordinationService(overflowPageResourceConfiguration: overflowPageResourceConfiguration), rowEncodingConfiguration: rowEncodingConfiguration with { OverflowSourceBound = true });

    public RowEncodingContext ReadRowEncodingContextProjectionType(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateProjectionType(rowEncodingContext: rowEncodingContext);

        rowEncodingContext.TargetType = rowEncodingContext.ProjectionTypeName switch
        {
            SqlTypeName.NVarChar => types.ProcessSqlTypeContextNVarCharMax63(sqlTypeContext: new SqlTypeContext())
            .SqlTypeResult,
            SqlTypeName.Xml => types.ProcessSqlTypeContextXml24(sqlTypeContext: new SqlTypeContext())
            .SqlTypeResult,
            SqlTypeName.Bit => types.ProcessSqlTypeContextBit7(sqlTypeContext: new SqlTypeContext())
            .SqlTypeResult,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(rowEncodingContext.ProjectionTypeName))};

        return rowEncodingContext;
    });

    public ValueTask FreeRowEncodingContextOverflowAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateReference(rowEncodingContext: rowEncodingContext, cancellationToken: cancellationToken);

        await storage.FreeRowEncodingContextOverflowAsync(rowEncodingContext: rowEncodingContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public RowEncodingContext ReadRowEncodingContext(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(rowEncodingContext: rowEncodingContext);
        rowEncodingContext.InlineThreshold = configuration.InlineThreshold;
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextEncode(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateRow(rowEncodingContext: rowEncodingContext);
        PrepareRow(rowEncodingContext: rowEncodingContext);
        formats.ProcessRowEncodingContextEncode(rowEncodingContext: rowEncodingContext);
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextDecode(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(rowEncodingContext: rowEncodingContext);
        formats.ProcessRowEncodingContextDecode(rowEncodingContext: rowEncodingContext);
        ValidateDecodedRow(rowEncodingContext: rowEncodingContext);
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextSelected(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateSelected(rowEncodingContext: rowEncodingContext);
        formats.ProcessRowEncodingContextDecodeSelected(rowEncodingContext: rowEncodingContext);
        ValidateSelectedValues(rowEncodingContext: rowEncodingContext);
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextLogicalEncode(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateValue(rowEncodingContext: rowEncodingContext);
        NormalizeValue(rowEncodingContext: rowEncodingContext);
        formats.ProcessRowEncodingContextLogicalEncode(rowEncodingContext: rowEncodingContext);
        return rowEncodingContext;
    });

    public RowEncodingContext ProcessRowEncodingContextUpdate(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateUpdate(rowEncodingContext: rowEncodingContext);
        ApplyInlineUpdate(rowEncodingContext: rowEncodingContext);
        return rowEncodingContext;
    });

    public RowEncodingContext ReadRowEncodingContextStorage(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateStorage(rowEncodingContext: rowEncodingContext);
        ReadStorage(rowEncodingContext: rowEncodingContext);
        return rowEncodingContext;
    });

    public RowEncodingContext ReadRowEncodingContextReferences(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(rowEncodingContext: rowEncodingContext);

        rowEncodingContext.ReferenceResults = ReadReferences(rowEncodingContext: rowEncodingContext)
            .ToDictionary(keySelector: pair => rowEncodingContext.Table!.Columns[pair.Key].Id, elementSelector: pair => pair.Value);

        return rowEncodingContext;
    });

    public ValueTask<RowEncodingContext> ProcessRowEncodingContextEncodeAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRowAsync(rowEncodingContext: rowEncodingContext, cancellationToken: cancellationToken);

        await EncodeOverflowAsync(rowEncodingContext: rowEncodingContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return rowEncodingContext;
    });

    public ValueTask<RowEncodingContext> ProcessRowEncodingContextDecodeAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateSourceAsync(rowEncodingContext: rowEncodingContext, cancellationToken: cancellationToken);

        await DecodeOverflowAsync(rowEncodingContext: rowEncodingContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return rowEncodingContext;
    });

    public ValueTask<RowEncodingContext> ProcessRowEncodingContextUpdateAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateUpdateAsync(rowEncodingContext: rowEncodingContext, cancellationToken: cancellationToken);

        await ApplyOverflowUpdateAsync(rowEncodingContext: rowEncodingContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return rowEncodingContext;
    });

    private void PrepareRow(RowEncodingContext rowEncodingContext)
    {
        storage.CheckRowEncodingContextMetadata(rowEncodingContext: rowEncodingContext);
        var values = rowEncodingContext.Row!.Values.ToArray();

        for (var index = 0; index < values.Length; index++)
        {
            var column = rowEncodingContext.Table!.Columns[index];
            ValidateTypedValue(column: column, value: values[index]);
            values[index] = NormalizeTypedValue(column: column, value: values[index]);
        }

        rowEncodingContext.Row = new Row
        {
            Values = values
        };
    }

    private void ValidateTypedValue(ColumnDefinition column, SqlValue value)
    {
        if (value.IsNull && !column.IsNullable)
        {
            throw new ArgumentException(message: $"Column '{column.Name}' does not allow SQL NULL.");
        }

        types.ProcessSqlTypeContextValidate76(sqlTypeContext: new SqlTypeContext { SqlTypeSource = column.Type, InputSqlValueValue = value, InputstringColumnName = column.Name });
    }

    private SqlValue NormalizeTypedValue(ColumnDefinition column, SqlValue value) =>
        types.ProcessSqlTypeContextNormalizeForStorage77(sqlTypeContext: new SqlTypeContext { SqlTypeSource = column.Type, InputSqlValueValue = value })
            .SqlValueResult!;

    private void NormalizeValue(RowEncodingContext rowEncodingContext)
    {
        ValidateTypedValue(column: rowEncodingContext.Column!, value: rowEncodingContext.Value!);
        rowEncodingContext.Value = NormalizeTypedValue(column: rowEncodingContext.Column!, value: rowEncodingContext.Value!);
    }

    private void ValidateDecodedRow(RowEncodingContext rowEncodingContext)
    {
        try
        {
            rowEncodingContext.Row = new Row
            {
                Values = rowEncodingContext.Row!.Values.Select(selector: MaterializeDecodedValue)
            .ToArray()
            };

            storage.CheckRowEncodingContextMetadata(rowEncodingContext: rowEncodingContext);

            for (var index = 0; index < rowEncodingContext.Table!.Columns.Count; index++)
            {
                ValidateTypedValue(column: rowEncodingContext.Table.Columns[index], value: rowEncodingContext.Row!.Values[index]);
            }
        }
        catch (ArgumentException exception)
        {
            throw new StorageFormatException(message: "Decoded row violates its declared schema.", innerException: exception);
        }
    }

    private void ValidateSelectedValues(RowEncodingContext rowEncodingContext)
    {
        try
        {
            rowEncodingContext.Selected = rowEncodingContext.Selected!.ToDictionary(keySelector: pair => pair.Key, elementSelector: pair => MaterializeDecodedValue(value: pair.Value));

            foreach (var pair in rowEncodingContext.Selected!)
            {
                var column = rowEncodingContext.Table!.Columns.Single(predicate: column => column.Id == pair.Key);
                ValidateTypedValue(column: column, value: pair.Value);
            }
        }
        catch (ArgumentException exception)
        {
            throw new StorageFormatException(message: "Decoded column violates its declared schema.", innerException: exception);
        }
    }

    private HashSet<int> ValidateUpdates(RowEncodingContext rowEncodingContext)
    {
        var indexes = new HashSet<int>();

        foreach (var update in rowEncodingContext.Update!.Columns)
        {
            if (update.ColumnIndex < 0 || update.ColumnIndex >= rowEncodingContext.Table!.Columns.Count)
            {
                throw new ArgumentOutOfRangeException(paramName: nameof(rowEncodingContext.Update), message: $"Unknown column index {update.ColumnIndex}.");
            }

            if (!indexes.Add(item: update.ColumnIndex))
            {
                throw new ArgumentException(message: $"Column index {update.ColumnIndex} is updated more than once.");
            }

            ValidateTypedValue(column: rowEncodingContext.Table.Columns[update.ColumnIndex], value: update.Value);
        }

        return indexes;
    }

    private void ApplyInlineUpdate(RowEncodingContext rowEncodingContext)
    {
        formats.ProcessRowEncodingContextDecode(rowEncodingContext: rowEncodingContext);
        ValidateDecodedRow(rowEncodingContext: rowEncodingContext);
        ValidateUpdates(rowEncodingContext: rowEncodingContext);
        var values = rowEncodingContext.Row!.Values.ToArray();

        foreach (var update in rowEncodingContext.Update!.Columns)
        {
            values[update.ColumnIndex] = update.Value;
        }

        rowEncodingContext.Row = new Row
        {
            Values = values
        };

        PrepareRow(rowEncodingContext: rowEncodingContext);
        formats.ProcessRowEncodingContextEncode(rowEncodingContext: rowEncodingContext);
    }

    private async ValueTask EncodeOverflowAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken)
    {
        PrepareRow(rowEncodingContext: rowEncodingContext);
        Dictionary<int, OverflowReference> references = new();
        List<OverflowReference> allocated = new();

        try
        {
            for (var index = 0; index < rowEncodingContext.Table!.Columns.Count; index++)
            {
                var column = rowEncodingContext.Table.Columns[index];
                var value = rowEncodingContext.Row!.Values[index];

                if (!formats.ProcessRowEncodingContextIsVariable(rowEncodingContext: new RowEncodingContext { Column = column })
            .IsVariable || value.IsNull)
                {
                    continue;
                }

                var bytes = formats.ProcessRowEncodingContextVariableEncode(rowEncodingContext: new RowEncodingContext { Column = column, Value = value })
            .Bytes!;

                if (bytes.Length <= configuration.InlineThreshold)
                {
                    continue;
                }

                var reference = (await storage.WriteRowEncodingContextOverflowAsync(rowEncodingContext: new RowEncodingContext { Source = bytes }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Reference!.Value;

                references.Add(key: index, value: reference);
                allocated.Add(item: reference);
            }

            rowEncodingContext.OverflowReferences = references;
            formats.ProcessRowEncodingContextEncode(rowEncodingContext: rowEncodingContext);

            rowEncodingContext.EncodingResult = new RowEncodingResult
            {
                Bytes = rowEncodingContext.Bytes!,
                NewlyAllocated = allocated.AsReadOnly(),
                Retired = Array.Empty<OverflowReference>()
            };
        }
        catch
        {
            await CleanupNewAsync(references: allocated)
            .ConfigureAwait(continueOnCapturedContext: false);

            throw;
        }
    }

    private async ValueTask DecodeOverflowAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken)
    {
        formats.ProcessRowEncodingContextHeader(rowEncodingContext: rowEncodingContext);
        formats.ProcessRowEncodingContextVariables(rowEncodingContext: rowEncodingContext);
        var values = new SqlValue[rowEncodingContext.Table!.Columns.Count];
        var fixedOffset = RowFormat.HeaderLength + rowEncodingContext.Header.NullBitmapLength;

        for (var index = 0; index < values.Length; index++)
        {
            var column = rowEncodingContext.Table.Columns[index];
            var isNull = (rowEncodingContext.Source.Span[RowFormat.HeaderLength + index / 8] & (1 << (index % 8))) != 0;

            if (isNull)
            {
                if (!column.IsNullable)
                {
                    throw new StorageFormatException(message: $"Non-nullable column '{column.Name}' is encoded as NULL.");
                }

                values[index] = new NullSqlValue
                {
                    IsNull = true
                };
            }
            else if (!formats.ProcessRowEncodingContextIsVariable(rowEncodingContext: new RowEncodingContext { Column = column })
            .IsVariable)
            {
                values[index] = formats.ProcessRowEncodingContextFixedDecode(rowEncodingContext: new RowEncodingContext { Column = column, Source = rowEncodingContext.Source[fixedOffset..] })
            .Value!;
            }
            else
            {
                var entry = rowEncodingContext.VariableEntries![index];
                var bytes = rowEncodingContext.Source.Slice(start: entry.Offset, length: entry.Length);

                if (entry.Storage == RowValueStorage.Overflow)
                {
                    bytes = (await storage.ReadRowEncodingContextOverflowAsync(rowEncodingContext: new RowEncodingContext { Reference = ReadReference(source: bytes.Span) }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Source;
                }
                else if (entry.Storage != RowValueStorage.Inline)
                {
                    throw new StorageFormatException(message: "Non-null variable value has invalid storage.");
                }

                values[index] = formats.ProcessRowEncodingContextVariableDecode(rowEncodingContext: new RowEncodingContext { Column = column, Source = bytes })
            .Value!;
            }

            fixedOffset = checked(fixedOffset + formats.ProcessRowEncodingContextWidth(rowEncodingContext: new RowEncodingContext { Column = column })
            .Width);
        }

        rowEncodingContext.Row = new Row
        {
            Values = values
        };

        ValidateDecodedRow(rowEncodingContext: rowEncodingContext);
    }

    private async ValueTask ApplyOverflowUpdateAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken)
    {
        await DecodeOverflowAsync(rowEncodingContext: rowEncodingContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var updatedIndexes = ValidateUpdates(rowEncodingContext: rowEncodingContext);
        var values = rowEncodingContext.Row!.Values.ToArray();

        foreach (var update in rowEncodingContext.Update!.Columns)
        {
            values[update.ColumnIndex] = update.Value;
        }

        rowEncodingContext.Row = new Row
        {
            Values = values
        };

        PrepareRow(rowEncodingContext: rowEncodingContext);
        var oldReferences = ReadReferences(rowEncodingContext: rowEncodingContext);
        Dictionary<int, OverflowReference> outputReferences = new();
        List<OverflowReference> allocated = new();
        List<OverflowReference> retired = new();

        try
        {
            foreach (var pair in oldReferences)
            {
                if (updatedIndexes.Contains(item: pair.Key))
                {
                    retired.Add(item: pair.Value);
                }
                else
                {
                    outputReferences.Add(key: pair.Key, value: pair.Value);
                }
            }

            foreach (var index in updatedIndexes)
            {
                var column = rowEncodingContext.Table!.Columns[index];
                var value = rowEncodingContext.Row!.Values[index];

                if (!formats.ProcessRowEncodingContextIsVariable(rowEncodingContext: new RowEncodingContext { Column = column })
            .IsVariable || value.IsNull)
                {
                    continue;
                }

                var bytes = formats.ProcessRowEncodingContextVariableEncode(rowEncodingContext: new RowEncodingContext { Column = column, Value = value })
            .Bytes!;

                if (bytes.Length <= configuration.InlineThreshold)
                {
                    continue;
                }

                var reference = (await storage.WriteRowEncodingContextOverflowAsync(rowEncodingContext: new RowEncodingContext { Source = bytes }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Reference!.Value;

                outputReferences.Add(key: index, value: reference);
                allocated.Add(item: reference);
            }

            rowEncodingContext.OverflowReferences = outputReferences;
            formats.ProcessRowEncodingContextEncode(rowEncodingContext: rowEncodingContext);

            rowEncodingContext.EncodingResult = new RowEncodingResult
            {
                Bytes = rowEncodingContext.Bytes!,
                NewlyAllocated = allocated.AsReadOnly(),
                Retired = retired.AsReadOnly()
            };
        }
        catch
        {
            await CleanupNewAsync(references: allocated)
            .ConfigureAwait(continueOnCapturedContext: false);

            throw;
        }
    }

    private Dictionary<int, OverflowReference> ReadReferences(RowEncodingContext rowEncodingContext)
    {
        formats.ProcessRowEncodingContextHeader(rowEncodingContext: rowEncodingContext);
        formats.ProcessRowEncodingContextVariables(rowEncodingContext: rowEncodingContext);
        Dictionary<int, OverflowReference> result = new();

        foreach (var pair in rowEncodingContext.VariableEntries!)
        {
            if (pair.Value.Storage == RowValueStorage.Overflow)
            {
                result.Add(key: pair.Key, value: ReadReference(source: rowEncodingContext.Source.Span.Slice(start: pair.Value.Offset, length: pair.Value.Length)));
            }
        }

        return result;
    }

    private static OverflowReference ReadReference(ReadOnlySpan<byte> source)
    {
        if (source.Length < OverflowReferenceFormat.EncodedLength)
        {
            throw new StorageFormatException(message: "Overflow reference is truncated.");
        }

        var reference = new OverflowReference
        {
            FirstPageId = new PageId
            {
                Value = BinaryPrimitives.ReadUInt64LittleEndian(source: source)
            },
            TotalLength = BinaryPrimitives.ReadInt64LittleEndian(source: source[8..])
        };

        if (reference.FirstPageId.Value == 0 || reference.TotalLength <= 0 || reference.TotalLength > OverflowReferenceFormat.MaximumValueLength)
        {
            throw new StorageFormatException(message: "Invalid overflow reference in a row.");
        }

        return reference;
    }

    private async ValueTask CleanupNewAsync(IReadOnlyList<OverflowReference> references)
    {
        for (var index = references.Count - 1; index >= 0; index--)
        {
            await storage.FreeRowEncodingContextOverflowAsync(rowEncodingContext: new RowEncodingContext { Reference = references[index] }, cancellationToken: default)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private void ReadStorage(RowEncodingContext rowEncodingContext)
    {
        var index = rowEncodingContext.Table!.Columns.Select(selector: (column, position) => (column, position))
            .Where(predicate: item => item.column.Id == rowEncodingContext.ColumnId)
            .Select(selector: item => item.position)
            .DefaultIfEmpty(defaultValue: -1)
            .Single();

        if (index < 0)
        {
            throw new ArgumentException(message: "Unknown column ID.", paramName: nameof(rowEncodingContext.ColumnId));
        }

        if (!formats.ProcessRowEncodingContextIsVariable(rowEncodingContext: new RowEncodingContext { Column = rowEncodingContext.Table.Columns[index] })
            .IsVariable)
        {
            rowEncodingContext.ValueStorage = RowValueStorage.Inline;
            return;
        }

        if ((rowEncodingContext.Source.Span[RowFormat.HeaderLength + index / 8] & (1 << (index % 8))) != 0)
        {
            rowEncodingContext.ValueStorage = RowValueStorage.Null;
            return;
        }

        formats.ProcessRowEncodingContextHeader(rowEncodingContext: rowEncodingContext);
        formats.ProcessRowEncodingContextVariables(rowEncodingContext: rowEncodingContext);
        rowEncodingContext.ValueStorage = rowEncodingContext.VariableEntries![index].Storage;
    }

    private SqlValue MaterializeDecodedValue(SqlValue value)
    {
        if (value is not VariantSqlValue variant)
        {
            return value;
        }

        var type = variant.DeclaredType;

        var restored = types.ProcessSqlTypeContextRestore73(sqlTypeContext: new SqlTypeContext { InputSqlTypeNameName = type.Name, InputbytePrecision = type.Precision, InputbyteScale = type.Scale, InputintLength = type.Length, InputboolIsMax = type.IsMax, InputstringCollation = type.Collation, InputushortVectorDimensions = type.VectorDimensions, InputSqlVectorBaseTypeVectorBaseType = type.VectorBaseType })
            .SqlTypeResult!;

        var column = new ColumnDefinition
        {
            Name = "sql_variant",
            Type = restored,
            IsNullable = false
        };

        ValidateTypedValue(column: column, value: variant.Value);

        return variant with
        {
            DeclaredType = restored,
            Value = NormalizeTypedValue(column: column, value: variant.Value)
        };
    }

    public RowEncodingContext ReadRowEncodingContextColumnSet(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateColumnSetText(rowEncodingContext: rowEncodingContext);
        return formats.ReadRowEncodingContextColumnSet(rowEncodingContext: rowEncodingContext);
    });

    public RowEncodingContext WriteRowEncodingContextColumnSet(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateColumnSetElements(rowEncodingContext: rowEncodingContext);
        return formats.WriteRowEncodingContextColumnSet(rowEncodingContext: rowEncodingContext);
    });
}