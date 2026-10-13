// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Managements.RowCodecs;

namespace sql_storage_engine.Exposures.Rows;
public sealed class RowCodecExposure : ICompositionExposure
{
    public const ushort FormatVersion = RowFormat.FormatVersion;
    public const int HeaderLength = RowFormat.HeaderLength;
    public const int MaximumEncodedLength = RowFormat.MaximumEncodedLength;
    public const int MaximumInlineValueLength = RowFormat.MaximumInlineValueLength;
    public const int VariableEntryLength = RowFormat.VariableEntryLength;
    private static readonly RowCodecContextManagementService service = RowCodecContextManagementService.CreateRowCodecContextManagementService(rowEncodingConfiguration: new RowEncodingConfiguration());
    private RowCodecExposure()
    {
    }

    public static byte[] Encode(Row row, TableDefinition tableDefinition) =>
        service.ProcessRowEncodingContextEncode(rowEncodingContext: new RowEncodingContext { Row = row, Table = tableDefinition })
            .Bytes!;

    public static Row Decode(ReadOnlySpan<byte> source, TableDefinition tableDefinition) =>
        service.ProcessRowEncodingContextDecode(rowEncodingContext: new RowEncodingContext { Source = source.ToArray(), Table = tableDefinition })
            .Row!;

    public static byte[] ApplyUpdate(ReadOnlySpan<byte> currentRow, RowUpdate rowUpdate, TableDefinition tableDefinition) =>
        service.ProcessRowEncodingContextUpdate(rowEncodingContext: new RowEncodingContext { Source = currentRow.ToArray(), Update = rowUpdate, Table = tableDefinition })
            .Bytes!;

    public static IReadOnlyDictionary<ColumnId, SqlValue> DecodeSelected(ReadOnlySpan<byte> source, TableDefinition tableDefinition, IEnumerable<ColumnId> columnIds) =>
        service.ProcessRowEncodingContextSelected(rowEncodingContext: new RowEncodingContext { Source = source.ToArray(), Table = tableDefinition, ColumnIds = columnIds })
            .Selected!;

    internal static byte[] EncodeLogicalValue(SqlValue sqlValue, ColumnDefinition columnDefinition) =>
        service.ProcessRowEncodingContextLogicalEncode(rowEncodingContext: new RowEncodingContext { Value = sqlValue, Column = columnDefinition })
            .Bytes!;

    internal static RowCodecBinding ReadRowCodecBinding() =>
        new RowCodecBinding
    {
        Service = service
    };
}