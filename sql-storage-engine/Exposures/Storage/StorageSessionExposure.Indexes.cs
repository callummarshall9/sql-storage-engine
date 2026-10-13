// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Exposures.Storage;
public sealed partial class StorageSessionExposure
{
    CatalogIndex IStorageIndex.ReadDefinition() =>
        indexContext.Index!.Definition!;

    public async ValueTask<IReadOnlyList<RowId>> FindAsync(IReadOnlyList<SqlValue> values, CancellationToken cancellationToken = default) =>
        (await service.ReadStorageSessionContextIndexAsync(storageSessionContext: new StorageSessionContext { Index = indexContext with { Operation = "Find", Values = values } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Index!.RowIds!;

    public async ValueTask<IReadOnlyList<StorageIndexEntry>> FindEntriesAsync(IReadOnlyList<SqlValue> values, CancellationToken cancellationToken = default) =>
        (await service.ReadStorageSessionContextIndexAsync(storageSessionContext: new StorageSessionContext { Index = indexContext with { Operation = "Entries", Values = values } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Index!.Entries!;

    public IAsyncEnumerable<RowId> ScanAsync(IReadOnlyList<SqlValue> lowerBound, IReadOnlyList<SqlValue> upperBound, bool includeLowerBound = true, bool includeUpperBound = true, ScanDirection direction = ScanDirection.Ascending, CancellationToken cancellationToken = default) =>
        service.ReadStorageSessionContextIndexScan(storageSessionContext: new StorageSessionContext { Index = indexContext with { Operation = "Values", LowerValues = lowerBound, UpperValues = upperBound, IncludeLower = includeLowerBound, IncludeUpper = includeUpperBound, Direction = direction } }, cancellationToken: cancellationToken)
            .Index!.RowStream!;

    public IAsyncEnumerable<RowId> ScanAsync(StorageIndexRange storageIndexRange, CancellationToken cancellationToken = default) =>
        service.ReadStorageSessionContextIndexScan(storageSessionContext: new StorageSessionContext { Index = indexContext with { Operation = "Range", Range = storageIndexRange } }, cancellationToken: cancellationToken)
            .Index!.RowStream!;

    public IAsyncEnumerable<StorageIndexEntry> ScanEntriesAsync(StorageIndexRange storageIndexRange, CancellationToken cancellationToken = default) =>
        service.ReadStorageSessionContextIndexScan(storageSessionContext: new StorageSessionContext { Index = indexContext with { Operation = "Entries", Range = storageIndexRange } }, cancellationToken: cancellationToken)
            .Index!.EntryStream!;

    public async ValueTask<IReadOnlyList<SpecializedIndexMatch>> SearchNearestAsync(SqlValue sqlValue, int count, CancellationToken cancellationToken = default) =>
        (await service.ReadStorageSessionContextIndexAsync(storageSessionContext: new StorageSessionContext { Index = indexContext with { Operation = "Nearest", Query = sqlValue, Count = count } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Index!.Matches!;

    public async ValueTask<IReadOnlyList<RowId>> FindJsonPathAsync(string path, SqlValue sqlValue, CancellationToken cancellationToken = default) =>
        (await service.ReadStorageSessionContextIndexAsync(storageSessionContext: new StorageSessionContext { Index = indexContext with { Operation = "JsonValue", Path = path, Query = sqlValue } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Index!.RowIds!;

    public async ValueTask<IReadOnlyList<RowId>> FindJsonPathExistsAsync(string path, CancellationToken cancellationToken = default) =>
        (await service.ReadStorageSessionContextIndexAsync(storageSessionContext: new StorageSessionContext { Index = indexContext with { Operation = "JsonExists", Path = path } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Index!.RowIds!;

    public async ValueTask<IReadOnlyList<RowId>> FindXmlPathAsync(string path, string? value = null, CancellationToken cancellationToken = default) =>
        (await service.ReadStorageSessionContextIndexAsync(storageSessionContext: new StorageSessionContext { Index = indexContext with { Operation = "Xml", Path = path, Text = value } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Index!.RowIds!;

    public IAsyncEnumerable<FullTextIndexMatch> SearchFullTextAsync(FullTextSearchRequest fullTextSearchRequest, CancellationToken cancellationToken = default) =>
        service.ReadStorageSessionContextIndexScan(storageSessionContext: new StorageSessionContext { Index = indexContext with { Operation = "FullText", FullText = fullTextSearchRequest } }, cancellationToken: cancellationToken)
            .Index!.FullTextStream!;
}