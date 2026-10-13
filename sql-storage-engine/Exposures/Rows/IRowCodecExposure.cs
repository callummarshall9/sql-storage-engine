// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Exposures.Rows;
public interface IRowCodecExposure
{
    RowCodecBinding ReadRowCodecBinding();

    int ReadInlineThreshold();

    ValueTask<RowEncodingResult> EncodeAsync(Row row, TableDefinition tableDefinition, CancellationToken cancellationToken = default);

    ValueTask<Row> DecodeAsync(ReadOnlyMemory<byte> source, TableDefinition tableDefinition, CancellationToken cancellationToken = default);

    ValueTask<RowEncodingResult> ApplyUpdateAsync(ReadOnlyMemory<byte> currentRow, RowUpdate rowUpdate, TableDefinition tableDefinition, CancellationToken cancellationToken = default);

    RowValueStorage GetStorage(ReadOnlySpan<byte> row, TableDefinition tableDefinition, ColumnId columnId);

    IReadOnlyDictionary<ColumnId, OverflowReference> GetOverflowReferences(ReadOnlySpan<byte> row, TableDefinition tableDefinition);
}