// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Catalog;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Tables;
using sql_storage_engine.Heap;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private IAsyncEnumerable<(RowId RowId, ReadOnlyMemory<byte> Row)> ScanHeapBytes(TableStorageContext tableStorageContext, CancellationToken cancellationToken) =>
        heapIndexMutations[tableStorageContext.MutationId].ScanHeapIndexMutationContext(heapIndexMutationContext: new HeapIndexMutationContext(), cancellationToken: cancellationToken)
            .Rows!;

    private IAsyncEnumerable<(RowId RowId, ReadOnlyMemory<byte> Row)> SampleHeapBytes(TableStorageContext tableStorageContext, decimal pagePercentage, long? repeatableSeed, CancellationToken cancellationToken) =>
        heapIndexMutations[tableStorageContext.MutationId].SampleHeapIndexMutationContext(heapIndexMutationContext: new HeapIndexMutationContext { PagePercentage = pagePercentage, RepeatableSeed = repeatableSeed }, cancellationToken: cancellationToken)
            .Rows!;

    private async ValueTask<long> CountHeapRowsAsync(TableStorageContext tableStorageContext, CancellationToken cancellationToken) =>
        (await heapIndexMutations[tableStorageContext.MutationId].CountHeapIndexMutationContextHeapAsync(heapIndexMutationContext: new HeapIndexMutationContext(), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).LiveRowCount;

    private async ValueTask<RowId> PersistHeapBytesAsync(TableStorageContext tableStorageContext, ReadOnlyMemory<byte> row, CancellationToken cancellationToken = default) =>
        (await heapIndexMutations[tableStorageContext.MutationId].AddHeapIndexMutationContextHeapAsync(newHeapIndexMutationContext: new HeapIndexMutationContext { Bytes = row }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).ResultRowId;

    private async ValueTask<bool> RemoveHeapBytesAsync(TableStorageContext tableStorageContext, RowId deletedRowId, CancellationToken cancellationToken = default) =>
        (await heapIndexMutations[tableStorageContext.MutationId].DeleteHeapIndexMutationContextHeapAsync(deletedHeapIndexMutationContext: new HeapIndexMutationContext { RowId = deletedRowId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Result;

    private async ValueTask<HeapUpdateResult> ReplaceHeapBytesAsync(TableStorageContext tableStorageContext, RowId updatedRowId, ReadOnlyMemory<byte> row, CancellationToken cancellationToken = default) =>
        (await heapIndexMutations[tableStorageContext.MutationId].UpdateHeapIndexMutationContextHeapAsync(updatedHeapIndexMutationContext: new HeapIndexMutationContext { RowId = updatedRowId, Bytes = row }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).UpdateResult;

    private async ValueTask<(TableHeapLookupResult Result, ReadOnlyMemory<byte> Row)> ReadHeapBytesAsync(TableStorageContext tableStorageContext, RowId rowId, CancellationToken cancellationToken)
    {
        var result = await heapIndexMutations[tableStorageContext.MutationId].ReadHeapIndexMutationContextHeapAsync(heapIndexMutationContext: new HeapIndexMutationContext { RowId = rowId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return (result.LookupResult, result.ResultBytes);
    }

    private async ValueTask<RowEncodingResult> EncodeTableRowAsync(TableStorageContext tableStorageContext, Row row, TableDefinition tableDefinition, CancellationToken cancellationToken) =>
        (await rowCodec.ProcessRowEncodingContextEncodeAsync(rowEncodingContext: new RowEncodingContext { Row = row, Table = tableDefinition }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EncodingResult!;

    private async ValueTask<Row> DecodeTableRowAsync(TableStorageContext tableStorageContext, ReadOnlyMemory<byte> source, TableDefinition tableDefinition, CancellationToken cancellationToken) =>
        (await rowCodec.ProcessRowEncodingContextDecodeAsync(rowEncodingContext: new RowEncodingContext { Source = source, Table = tableDefinition }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Row!;

    private async ValueTask<RowEncodingResult> EncodeTableRowUpdateAsync(TableStorageContext tableStorageContext, ReadOnlyMemory<byte> currentRow, RowUpdate rowUpdate, TableDefinition tableDefinition, CancellationToken cancellationToken) =>
        (await rowCodec.ProcessRowEncodingContextUpdateAsync(rowEncodingContext: new RowEncodingContext { Source = currentRow, Update = rowUpdate, Table = tableDefinition }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EncodingResult!;

    private IReadOnlyDictionary<ColumnId, sql_storage_engine.Models.Overflow.OverflowReference> ReadTableOverflowReferences(TableStorageContext tableStorageContext, ReadOnlySpan<byte> row, TableDefinition tableDefinition) =>
        rowCodec.ReadRowEncodingContextReferences(rowEncodingContext: new RowEncodingContext { Source = row.ToArray(), Table = tableDefinition })
            .ReferenceResults!;

    private ValueTask FreeTableOverflowAsync(TableStorageContext tableStorageContext, sql_storage_engine.Models.Overflow.OverflowReference overflowReference, CancellationToken cancellationToken = default) =>
        rowCodec.FreeRowEncodingContextOverflowAsync(rowEncodingContext: new RowEncodingContext { Reference = overflowReference }, cancellationToken: cancellationToken);

    private ValueTask PersistIndexReferenceAsync(TableStorageContext tableStorageContext, TableIndexBinding tableIndexBinding, IndexKey newIndexKey, RowId newRowId, ReadOnlyMemory<byte> includedPayload, CancellationToken token) =>
        new ValueTask(task: heapIndexMutations[tableStorageContext.MutationId].AddHeapIndexMutationContextIndexAsync(newHeapIndexMutationContext: new HeapIndexMutationContext { Index = tableIndexBinding, Key = newIndexKey, RowId = newRowId, IncludedPayload = includedPayload }, cancellationToken: token)
            .AsTask());

    private async ValueTask<bool> RemoveIndexReferenceAsync(TableStorageContext tableStorageContext, TableIndexBinding tableIndexBinding, IndexKey indexKey, RowId rowId, CancellationToken token = default) =>
        (await heapIndexMutations[tableStorageContext.MutationId].DeleteHeapIndexMutationContextIndexAsync(deletedHeapIndexMutationContext: new HeapIndexMutationContext { Index = tableIndexBinding, Key = indexKey, RowId = rowId }, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false)).Result;

    private async ValueTask<IReadOnlyList<RowId>> FindIndexReferencesAsync(TableStorageContext tableStorageContext, TableIndexBinding tableIndexBinding, IndexKey indexKey, CancellationToken cancellationToken) =>
        (await heapIndexMutations[tableStorageContext.MutationId].FindHeapIndexMutationContextIndexAsync(heapIndexMutationContext: new HeapIndexMutationContext { Index = tableIndexBinding, Key = indexKey }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).IndexRows!;

    private IndexKey EncodeTableIndexKey(Row row, CatalogTable catalogTable, CatalogIndex catalogIndex) =>
        catalogIndexKeys.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 7, Row = row, Table = catalogTable, Index = catalogIndex })
            .Key!;

    private SqlValue EvaluateTableExpression(string expression, IReadOnlyDictionary<string, SqlValue> columns) =>
        expressions.EvaluateSqlExpressionContext(sqlExpressionContext: new SqlExpressionContext { Source = expression, Columns = columns })
            .Result!;

    private IReadOnlyList<int> ReadComputedColumnOrder(IReadOnlyList<CatalogColumn> columns) =>
        expressions.ReadSqlExpressionContextComputedOrder(sqlExpressionContext: new SqlExpressionContext { ColumnDefinitions = columns })
            .ComputedColumnOrder!;

    private CatalogColumn? ResolveComputedColumnReference(string expression, IReadOnlyList<CatalogColumn> columns) =>
        expressions.ReadSqlExpressionContextColumnReference(sqlExpressionContext: new SqlExpressionContext { Source = expression, ColumnDefinitions = columns })
            .DirectColumnReference;

    private SqlValue ConvertTableValue(SqlType sqlType, SqlValue sqlValue) =>
        rowCodec.ProcessRowEncodingContextConversion(rowEncodingContext: new RowEncodingContext { TargetType = sqlType, Value = sqlValue })
            .Value!;

    private SqlValue ConvertTableValue(SqlType[] sqlTypes, SqlValue sqlValue) =>
        rowCodec.ProcessRowEncodingContextConversion(rowEncodingContext: new RowEncodingContext { TargetType = sqlTypes[0], SourceType = sqlTypes[1], Value = sqlValue })
            .Value!;

    private SqlType ReadProjectionType(SqlTypeName sqlTypeName) =>
        rowCodec.ReadRowEncodingContextProjectionType(rowEncodingContext: new RowEncodingContext { ProjectionTypeName = sqlTypeName })
            .TargetType!;

    private void AdmitTableRow(TableDefinition tableDefinition, Row row) =>
        rowCodec.ProcessRowEncodingContextAdmission(rowEncodingContext: new RowEncodingContext { Table = tableDefinition, Row = row });

    private async ValueTask<SqlValue> AllocateTableRowVersionAsync(TableStorageContext tableStorageContext, CancellationToken cancellationToken) =>
        (await (generations ?? throw new InvalidOperationException(message: "This table requires a database generation allocator.")).AllocateStorageGenerationContextRowVersionAsync(storageGenerationContext: new StorageGenerationContext(), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Value!;

    private async ValueTask<SqlValue> AllocateTableGeneratedValueAsync(TableStorageContext tableStorageContext, CatalogGeneratedAlwaysKind kind, CancellationToken cancellationToken) =>
        (await (generations ?? throw new InvalidOperationException(message: "This table requires a database generation allocator.")).AllocateStorageGenerationContextGeneratedValueAsync(storageGenerationContext: new StorageGenerationContext { Kind = kind }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Value!;
}