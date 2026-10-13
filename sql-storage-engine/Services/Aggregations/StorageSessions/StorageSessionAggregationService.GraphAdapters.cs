// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Graph;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Tables;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private void EnsureGraphCurrent(StorageGraphContext graphContext) =>
        EnsureStorageIndexCurrent(storageIndexContext: new StorageIndexContext { Handle = graphContext.Handle });

    private ValueTask<EngineResourceContext> EnterGraphScopeAsync(StorageGraphContext graphContext, CancellationToken cancellationToken) =>
        EnterStorageIndexScopeAsync(storageIndexContext: new StorageIndexContext { Handle = graphContext.Handle }, cancellationToken: cancellationToken, reentrant: graphContext.Handle!.Statement is not null);

    private ValueTask PublishGraphAsync(StorageGraphContext graphContext, CancellationToken cancellationToken) =>
        graphContext.Handle!.Statement is null ? FlushPublicEngineAsync(storageEngineRuntime: graphContext.Handle.Engine!, cancellationToken: cancellationToken) : ValueTask.CompletedTask;

    private DatabaseId ReadGraphDatabaseId(StorageGraphContext graphContext) =>
        engineResources.ReadEngineResourceContext(engineResourceContext: (EngineResourceContext)graphContext.Handle!.Engine!.ResourceSource!)
            .Header!.DatabaseId;

    private Guid CreateGuidGraphIdentifier() =>
        expressions.CreateSqlExpressionContextIdentifier(sqlExpressionContext: new SqlExpressionContext())
            .Identifier;

    private GraphRowContext ProcessGraphRow(GraphRowContext graphRowContext)
    {
        try
        {
            return rowCodec.ProcessRowEncodingContextGraph(rowEncodingContext: new RowEncodingContext { Graph = graphRowContext })
            .Graph!;
        }
        catch (sql_storage_engine.Models.Exceptions.StorageModelValidationException exception)
        {
            throw new ArgumentException(message: exception.Message, paramName: exception.ParamName, innerException: exception);
        }
    }

    private GraphNodeId CreateGraphNodeId(DatabaseId databaseId, TableId tableId, ulong schemaVersion, Guid value) =>
        ProcessGraphRow(graphRowContext: new GraphRowContext { Operation = "Prepare", Identity = new GraphIdentityValue { Identity = new GraphIdentity { Kind = 1, DatabaseId = databaseId.Value, TableId = tableId.Value, SchemaVersion = schemaVersion, Value = value } } })
            .Identity!.NodeId;

    private GraphEdgeId CreateGraphEdgeId(DatabaseId databaseId, TableId tableId, ulong schemaVersion, Guid value) =>
        ProcessGraphRow(graphRowContext: new GraphRowContext { Operation = "Prepare", Identity = new GraphIdentityValue { Identity = new GraphIdentity { Kind = 2, DatabaseId = databaseId.Value, TableId = tableId.Value, SchemaVersion = schemaVersion, Value = value } } })
            .Identity!.EdgeId;

    private SqlValue EncodeGraphNodeId(GraphNodeId graphNodeId) =>
        ProcessGraphRow(graphRowContext: new GraphRowContext { Operation = "Encode", Identity = new GraphIdentityValue { Identity = new GraphIdentity { Kind = 1, DatabaseId = graphNodeId.DatabaseId.Value, TableId = graphNodeId.TableId.Value, SchemaVersion = graphNodeId.SchemaVersion, Value = graphNodeId.Value } } })
            .Identity!.Value!;

    private SqlValue EncodeGraphEdgeId(GraphEdgeId graphEdgeId) =>
        ProcessGraphRow(graphRowContext: new GraphRowContext { Operation = "Encode", Identity = new GraphIdentityValue { Identity = new GraphIdentity { Kind = 2, DatabaseId = graphEdgeId.DatabaseId.Value, TableId = graphEdgeId.TableId.Value, SchemaVersion = graphEdgeId.SchemaVersion, Value = graphEdgeId.Value } } })
            .Identity!.Value!;

    private GraphNodeId DecodeGraphNodeId(SqlValue sqlValue) =>
        ProcessGraphRow(graphRowContext: new GraphRowContext { Operation = "Decode", Identity = new GraphIdentityValue { Value = sqlValue, Identity = new GraphIdentity { Kind = 1, ParameterName = "value" } } })
            .Identity!.NodeId;

    private GraphEdgeId DecodeGraphEdgeId(SqlValue sqlValue) =>
        ProcessGraphRow(graphRowContext: new GraphRowContext { Operation = "Decode", Identity = new GraphIdentityValue { Value = sqlValue, Identity = new GraphIdentity { Kind = 2, ParameterName = "value" } } })
            .Identity!.EdgeId;

    private void ValidateGraphNodeOwnership(GraphNodeId nodeId, DatabaseId databaseId, CatalogTable table, string parameterName) =>
        ProcessGraphRow(graphRowContext: new GraphRowContext { Operation = "ValidateNode", NodeId = nodeId, DatabaseId = databaseId, Table = table, ParameterName = parameterName });

    private void ValidateGraphEdgeOwnership(GraphEdgeId edgeId, DatabaseId databaseId, CatalogTable table, string parameterName) =>
        ProcessGraphRow(graphRowContext: new GraphRowContext { Operation = "ValidateEdge", EdgeId = edgeId, DatabaseId = databaseId, Table = table, ParameterName = parameterName });

    private void ValidateGraphPayload(Row row, CatalogTable table, int generatedCount, string parameterName) =>
        ProcessGraphRow(graphRowContext: new GraphRowContext { Operation = "ValidatePayload", Row = row, Table = table, GeneratedCount = generatedCount, ParameterName = parameterName });

    private void ValidateGraphPayloadUpdate(RowUpdate update, int payloadColumnCount) =>
        ProcessGraphRow(graphRowContext: new GraphRowContext { Operation = "ValidateUpdate", Update = update, PayloadColumnCount = payloadColumnCount, ParameterName = nameof(update) });

    private Row AppendGraphPayload(Row payload, params SqlValue[] generated) =>
        ProcessGraphRow(graphRowContext: new GraphRowContext { Operation = "Append", Row = payload, Generated = generated })
            .Row!;

    private Row ReadGraphPayload(Row stored, int generatedCount) =>
        ProcessGraphRow(graphRowContext: new GraphRowContext { Operation = "Payload", Row = stored, GeneratedCount = generatedCount })
            .Row!;

    private static int CompareGraphRowIds(RowId left, RowId right)
    {
        var page = left.PageId.Value.CompareTo(value: right.PageId.Value);

        if (page != 0)
        {
            return page;
        }

        var slot = left.SlotId.Value.CompareTo(value: right.SlotId.Value);
        return slot != 0 ? slot : left.Generation.Value.CompareTo(value: right.Generation.Value);
    }

    private async ValueTask<RowId?> FindGraphUniqueAsync(StorageGraphContext graphContext, CatalogTable table, IndexId indexId, SqlValue identity, CancellationToken cancellationToken)
    {
        var index = ReadCatalogIndexBinding(indexId: indexId);
        var key = PublicIndexKeyEncodeValues(values: [identity], catalogTable: table, catalogIndex: index.Definition!);

        var matches = await ReadPublicIndexRowsAsync(storageIndexContext: new StorageIndexContext { Handle = graphContext.Handle, Index = index, Table = table }, indexKey: key, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return matches.Count switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new StorageCorruptionException(message: "A unique graph identity index returned duplicate rows.")};
    }

    private IndexRange ReadGraphExactRange(CatalogTable table, CatalogIndex index, SqlValue value)
    {
        var key = PublicIndexKeyEncodeValues(values: [value], catalogTable: table, catalogIndex: index);

        return new IndexRange
        {
            LowerBound = key,
            UpperBound = key,
            IncludeLowerBound = true,
            IncludeUpperBound = true,
            Direction = ScanDirection.Ascending
        };
    }

    private IAsyncEnumerable<LeafIndexEntry> ScanGraphIndexEntries(StorageGraphContext graphContext, CatalogIndex index, IndexRange range, CancellationToken cancellationToken) =>
        ScanPublicIndexEntries(storageIndexContext: new StorageIndexContext { Handle = graphContext.Handle, Index = ReadCatalogIndexBinding(indexId: index.Id), Table = graphContext.Storage!.Definition }, indexRange: range, cancellationToken: cancellationToken);

    private async ValueTask<TableInsertResult> InsertGraphRowAsync(TableStorageContext tableStorageContext, Row row, CancellationToken cancellationToken) =>
        (await AddStorageSessionContextTableGraphAsync(newStorageSessionContext: new StorageSessionContext { Table = tableStorageContext with { Binding = tableStorageContext, Row = row } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.InsertResult!;

    private async ValueTask<TableUpdateResult> UpdateGraphRowAsync(TableStorageContext tableStorageContext, RowId rowId, RowUpdate update, CancellationToken cancellationToken) =>
        (await UpdateStorageSessionContextTableGraphAsync(updatedStorageSessionContext: new StorageSessionContext { Table = tableStorageContext with { Binding = tableStorageContext, RowId = rowId, Update = update } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.UpdateResult!;
}