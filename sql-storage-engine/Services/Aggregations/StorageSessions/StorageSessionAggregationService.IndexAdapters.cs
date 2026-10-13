// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Catalog;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Tables;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private void EnsureStorageIndexCurrent(StorageIndexContext storageIndexContext)
    {
        var handle = storageIndexContext.Handle!;

        if (handle.Statement is not null && !handle.Statement.Active)
        {
            throw new InvalidOperationException(message: "The statement scope has completed.");
        }

        if (handle.Engine!.Disposed)
        {
            throw new ObjectDisposedException(objectName: "StorageEngineExposure");
        }

        if (handle.Engine.Quarantined)
        {
            throw new InvalidOperationException(message: "Storage remains quarantined while transaction cleanup is pending.");
        }

        if (handle.Generation != handle.Engine.Generation)
        {
            throw new InvalidOperationException(message: "The storage handle is stale after statement rollback; reopen it from the catalog.");
        }
    }

    private async ValueTask<EngineResourceContext> EnterStorageIndexScopeAsync(StorageIndexContext storageIndexContext, CancellationToken cancellationToken, bool reentrant)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);
        var resources = (EngineResourceContext)storageIndexContext.Handle!.Engine!.ResourceSource!;

        var entered = await engineResources.EnterEngineResourceContextAsync(engineResourceContext: resources with { Reentrant = reentrant }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);
            return entered;
        }
        catch
        {
            entered.Lifetime!.Dispose();
            throw;
        }
    }

    private EngineResourceContext ActivateStorageIndexScope(StorageIndexContext storageIndexContext, EngineResourceContext engineResourceContext) =>
        engineResources.ActivateEngineResourceContext(engineResourceContext: engineResourceContext);

    private async ValueTask<IReadOnlyList<LeafIndexEntry>> ReadPublicIndexEntriesAsync(StorageIndexContext storageIndexContext, IndexKey indexKey, CancellationToken cancellationToken) =>
        (await catalogStorage.ReadCatalogStorageContextIndexEntriesAsync(catalogStorageContext: new CatalogStorageContext { TreeBinding = new IndexTreeBinding { Service = storageIndexContext.Index!.Service }, Tree = storageIndexContext.Index.Context!with { Binding = storageIndexContext.Index.Context, Key = indexKey } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Entries!;

    private async ValueTask<IReadOnlyList<RowId>> ReadPublicIndexRowsAsync(StorageIndexContext storageIndexContext, IndexKey indexKey, CancellationToken cancellationToken) =>
        (await ReadPublicIndexEntriesAsync(storageIndexContext: storageIndexContext, indexKey: indexKey, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Select(selector: entry => entry.RowId)
            .ToArray();

    private IAsyncEnumerable<LeafIndexEntry> ScanPublicIndexEntries(StorageIndexContext storageIndexContext, IndexRange indexRange, CancellationToken cancellationToken) =>
        catalogStorage.ReadCatalogStorageContextIndexScan(catalogStorageContext: new CatalogStorageContext { TreeBinding = new IndexTreeBinding { Service = storageIndexContext.Index!.Service }, Tree = storageIndexContext.Index.Context!with { Binding = storageIndexContext.Index.Context, Range = indexRange } }, cancellationToken: cancellationToken)
            .EntryStream!;

    private async ValueTask<(bool Found, Row? Row)> ReadPublicTableRowAsync(TableStorageContext tableStorageContext, RowId rowId, CancellationToken cancellationToken)
    {
        var result = await ReadStorageSessionContextTableAsync(storageSessionContext: new StorageSessionContext { Table = tableStorageContext with { Binding = tableStorageContext, RowId = rowId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return (result.Table!.Found, result.Table.Row);
    }

    private IndexKey PublicIndexKeyEncodeValues(IReadOnlyList<SqlValue> values, CatalogTable catalogTable, CatalogIndex catalogIndex) =>
        catalogIndexKeys.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 10, Values = values, Table = catalogTable, Index = catalogIndex })
            .Key!;

    private IndexKey PublicIndexKeyEncodePrefix(IReadOnlyList<SqlValue> values, CatalogTable catalogTable, CatalogIndex catalogIndex) =>
        catalogIndexKeys.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 11, Values = values, Table = catalogTable, Index = catalogIndex })
            .Key!;

    private IReadOnlyList<IndexKey> PublicJsonPathValueIndexKeys(CatalogIndex catalogIndex, string path, SqlValue sqlValue) =>
        catalogIndexKeys.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 12, Index = catalogIndex, Path = path, SqlValue = sqlValue })
            .Entries!;

    private IReadOnlyList<IndexKey> PublicJsonPathExistsIndexKeys(CatalogIndex catalogIndex, string path) =>
        catalogIndexKeys.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 13, Index = catalogIndex, Path = path })
            .Entries!;

    private IndexKey PublicIndexKeyEncodeXmlPathValue(CatalogIndex catalogIndex, string path, string value) =>
        catalogIndexKeys.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 4, Index = catalogIndex, Path = path, Value = value })
            .Key!;

    private IndexKey PublicIndexKeyEncodeXmlPathExists(CatalogIndex catalogIndex, string path) =>
        catalogIndexKeys.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 5, Index = catalogIndex, Path = path })
            .Key!;

    private IndexKey PublicIndexKeyEncodeFullTextTerm(CatalogIndex catalogIndex, CatalogTable catalogTable, FullTextSearchRequest request) =>
        catalogIndexKeys.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 6, Index = catalogIndex, Table = catalogTable, Request = request })
            .Key!;

    private IReadOnlyDictionary<ColumnId, SqlValue> PublicIndexKeyDecodeIncludedValues(ReadOnlySpan<byte> payload, CatalogTable catalogTable, CatalogIndex catalogIndex) =>
        catalogIndexKeys.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 9, Payload = payload.ToArray(), Index = catalogIndex, Table = catalogTable })
            .IncludedValues!;

    private static ReadOnlyMemory<byte> ReadPublicIndexKeyBytes(IndexKey indexKey) =>
        indexKey.Content.Select(selector: value => (byte)value)
            .ToArray();

    private static IndexKey CreatePublicIndexKey(ReadOnlySpan<byte> bytes) =>
        new IndexKey
    {
        Content = new string (value: bytes.ToArray()
            .Select(selector: value => (char)value)
            .ToArray())
    };

    private static ReadOnlyMemory<byte> ReadPublicIndexEntryPayload(LeafIndexEntry leafIndexEntry) =>
        leafIndexEntry.PayloadContent.Select(selector: value => (byte)value)
            .ToArray();

    private void ValidatePublicIndexValue(SqlType sqlType, SqlValue sqlValue, string columnName) =>
        sqlValues.ValidateSqlValueRequestValue(sqlValueRequest: new SqlValueRequest { Input = sqlType, Other = sqlValue, Third = columnName });

    private double MeasurePublicIndexVectorDistance(VectorSqlValue[] vectorSqlValues, SqlVectorDistanceMetric metric) =>
        sqlValues.MeasureVectorSqlValueRequest(sqlValueRequest: new SqlValueRequest { SqlValues = vectorSqlValues, Other = metric })
            .DistanceResult;

    private double MeasurePublicIndexSpatialDistance(SqlValue[] sqlValues) =>
        this.sqlValues.MeasureSpatialSqlValueRequest(sqlValueRequest: new SqlValueRequest { SqlValues = sqlValues })
            .DistanceResult;

    private static StorageIndexEntry CreatePublicStorageIndexEntry(RowId rowId, IReadOnlyDictionary<ColumnId, SqlValue> includedValues) =>
        new StorageIndexEntry
    {
        RowId = rowId,
        IncludedValues = new ReadOnlyDictionary<ColumnId, SqlValue>(dictionary: new Dictionary<ColumnId, SqlValue>(collection: includedValues))
    };

    private IAsyncEnumerable<T> TrackPublicIndexStream<T>(StorageIndexContext storageIndexContext, IAsyncEnumerable<T> source)
    {
        var resources = (EngineResourceContext)storageIndexContext.Handle!.Engine!.ResourceSource!;

        var scope = storageIndexContext.Handle.Statement is null ? null : engineResources.ReadEngineResourceContextAccess(engineResourceContext: resources)
            .Access;

        return (IAsyncEnumerable<T>)engineResources.TrackEngineResourceContextStream<T>(engineResourceContext: resources with { StreamSource = source, Access = scope })
            .StreamSource!;
    }
}