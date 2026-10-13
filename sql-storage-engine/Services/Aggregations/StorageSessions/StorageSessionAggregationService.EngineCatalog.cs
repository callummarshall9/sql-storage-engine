// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Tables;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    public StorageSessionContext ReadStorageSessionContextEngineCatalog(StorageSessionContext storageSessionContext) =>
        TryCatch(operation: () =>
    {
        ValidateEngineCatalogRequest(storageSessionContext: storageSessionContext, cancellationToken: CancellationToken.None, reading: true);
        var engine = storageSessionContext.Engine!;

        if (storageSessionContext.EngineCatalog!.Operation == 7)
        {
            if (engine.Disposed)
            {
                throw new ObjectDisposedException(objectName: "StorageSessionExposure");
            }

            var header = ((EngineResourceContext)engine.ResourceSource!).Header!;
            storageSessionContext.EngineCatalog.DatabaseId = header.DatabaseId;
            storageSessionContext.EngineCatalog.DefaultCollation = header.DefaultCollation;
            return storageSessionContext;
        }

        var gate = engineResources.EnterImmediateEngineResourceContext(engineResourceContext: ((EngineResourceContext)engine.ResourceSource!)with { Reentrant = storageSessionContext.Statement is not null });
        using var lease = gate.Lifetime;

        using var scope = engineResources.ActivateEngineResourceContext(engineResourceContext: gate)
            .Lifetime;

        EnsureEngineCatalogCurrent(storageEngineRuntime: engine);
        var context = storageSessionContext.EngineCatalog!;
        var resources = engineResources.ReadEngineResourceContext(engineResourceContext: ((EngineResourceContext)engine.ResourceSource!)with { Reentrant = storageSessionContext.Statement is not null });
        context.DatabaseId = resources.Header!.DatabaseId;
        context.DefaultCollation = resources.Header.DefaultCollation;
        context.Definition = catalog.Definition;

        switch (context.Operation)
        {
            case 0:
                break;
            case 1:
                ExecuteCatalogRequestAsync(storageSessionContext: new StorageSessionContext { Catalog = context.Request }, cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult();
                break;
            case 2:
                context.Indexes = catalog.Definition.Indexes.Where(predicate: index => index.TableId == context.TableId)
            .ToArray();
                break;
            case 3:
                context.Table = TryOpenTable(tableId: context.TableId, table: out var current) && current!.SystemVersioning is { } temporal && TryOpenTable(tableId: temporal.HistoryTableId, table: out var history) ? history : null;
                context.Found = context.Table is not null;
                break;
            case 4:
                context.Table = catalog.Definition.Tables.SingleOrDefault(predicate: table => table.SystemVersioning?.HistoryTableId == context.TableId);
                context.Found = context.Table is not null;
                break;
        }

        return storageSessionContext;
    });

    public ValueTask<StorageSessionContext> ExecuteStorageSessionContextEngineCatalogAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateEngineCatalogRequest(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken, reading: false);
        var engine = storageSessionContext.Engine!;
        EnsureEngineCatalogCurrent(storageEngineRuntime: engine);

        var gate = await engineResources.EnterEngineResourceContextAsync(engineResourceContext: ((EngineResourceContext)engine.ResourceSource!)with { Reentrant = storageSessionContext.Statement is not null }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lease = gate.Lifetime;

        using var scope = engineResources.ActivateEngineResourceContext(engineResourceContext: gate)
            .Lifetime;

        EnsureEngineCatalogCurrent(storageEngineRuntime: engine);
        var context = storageSessionContext.EngineCatalog!;

        switch (context.Operation)
        {
            case 1:
                var request = context.Request!;
                var resources = engineResources.ReadEngineResourceContext(engineResourceContext: ((EngineResourceContext)engine.ResourceSource!)with { Reentrant = storageSessionContext.Statement is not null });
                if (request.Operation is 1 or 2 or 3 or 4 or 17 or 18)
                {
                    request.Columns = ApplyEngineDatabaseCollation(columns: request.Columns!, defaultCollation: resources.Header!.DefaultCollation);
                    request.SchemaVersion = 1;
                    context.Request = request;
                }

                if (request.Operation == 16)
                {
                    request.TypeDefinition = catalogIndexKeys.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 15, Type = request.TypeDefinition, DefaultCollation = resources.Header!.DefaultCollation })
            .Type;
                }

                if (request.Operation == 4)
                {
                    ValidateTemporalCatalogNames(request: request);
                    var names = request.QualifiedNames!;

                    if (names[1] is null)
                    {
                        names[1] = new CatalogTableName
                        {
                            DatabaseName = names[0]!.DatabaseName,
                            SchemaName = names[0]!.SchemaName,
                            Name = names[0]!.Name + "History"
                        };
                    }
                }

                if (request.Operation is 9 or 10 or 102)
                {
                    await EnsureEngineCatalogTableEmptyAsync(tableId: request.TableId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                }

                await ExecuteCatalogRequestAsync(storageSessionContext: new StorageSessionContext { Catalog = request }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                if (catalog.RootPageId is not { } root)
                {
                    throw new InvalidOperationException(message: "Catalog publication produced no root.");
                }

                await engineResources.PublishCatalogRootEngineResourceContextAsync(engineResourceContext: resources with { PageId = root.Value }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                engine.ResourceSource = engineResources.ReadEngineResourceContext(engineResourceContext: resources);
                if (request.Operation is 9 or 10 or 102)
                {
                    engine.Generation = checked(engine.Generation + 1);
                }

                break;
            case 5:
                context.TableStatistics = await ReadStorageTableStatisticsAsync(tableId: context.TableId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 6:
                context.IndexStatistics = await ReadStorageIndexStatisticsAsync(indexId: context.IndexId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
        }

        context.Definition = catalog.Definition;
        return storageSessionContext;
    });

    private IEnumerable<CatalogColumn> ApplyEngineDatabaseCollation(IEnumerable<CatalogColumn> columns, string defaultCollation)
    {
        ArgumentNullException.ThrowIfNull(argument: columns);

        return columns.Select(selector: column =>
        {
            var type = catalogIndexKeys.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 15, Type = column.Type, DefaultCollation = defaultCollation })
            .Type!;

            return column with
            {
                Type = type,
                TypeSource = type
            };
        });
    }

    private async ValueTask EnsureEngineCatalogTableEmptyAsync(TableId tableId, CancellationToken cancellationToken)
    {
        var storage = await ReadCatalogTableStorageContextAsync(tableId: tableId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await using var rows = ScanTableRowsAsync(tableStorageContext: storage, cancellationToken: cancellationToken)
            .GetAsyncEnumerator(cancellationToken: cancellationToken);

        if (await rows.MoveNextAsync()
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            throw new InvalidOperationException(message: "A table must be empty before graph metadata is registered.");
        }
    }

    private async ValueTask<IReadOnlyList<PageId>> ReadCatalogHeapPageIdsAsync(TableStorageContext tableStorageContext, CancellationToken cancellationToken) =>
        (await catalogStorage.ReadCatalogStorageContextHeapPagesAsync(catalogStorageContext: new CatalogStorageContext { HeapBinding = tableStorageContext.HeapBinding }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).PageIds!;

    private async ValueTask<IReadOnlyList<PageId>> ReadCatalogTreePageIdsAsync(IndexTreeBinding indexTreeBinding, CancellationToken cancellationToken) =>
        (await catalogStorage.ReadCatalogStorageContextIndexPagesAsync(catalogStorageContext: new CatalogStorageContext { TreeBinding = indexTreeBinding }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).PageIds!;

    private IAsyncEnumerable<LeafIndexEntry> ReadCatalogTreeRows(IndexTreeBinding indexTreeBinding, IndexRange indexRange, CancellationToken cancellationToken) =>
        catalogStorage.ReadCatalogStorageContextIndexScan(catalogStorageContext: new CatalogStorageContext { TreeBinding = indexTreeBinding, Tree = new IndexTreeContext { Range = indexRange } }, cancellationToken: cancellationToken)
            .EntryStream!;

    private async ValueTask<StorageTableStatistics> ReadStorageTableStatisticsAsync(TableId tableId, CancellationToken cancellationToken = default)
    {
        if (!TryOpenTable(tableId: tableId, table: out var definition))
        {
            throw new KeyNotFoundException(message: $"Unknown table {tableId}.");
        }

        var storage = await ReadCatalogTableStorageContextAsync(tableId: tableId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var nullCounts = new long[definition!.Columns.Count];

        var distinct = definition.Columns.Select(selector: _ => new HashSet<SqlValue>())
            .ToArray();

        long rowCount = 0;

        await foreach (var stored in ScanTableRowsAsync(tableStorageContext: storage, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            rowCount++;

            for (var index = 0; index < stored.Row.Values.Count; index++)
            {
                var value = stored.Row.Values[index];

                if (value.IsNull)
                {
                    nullCounts[index]++;
                }
                else
                {
                    distinct[index].Add(item: value);
                }
            }
        }

        var pages = await ReadCatalogHeapPageIdsAsync(tableStorageContext: storage, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return new StorageTableStatistics
        {
            TableId = tableId,
            RowCount = rowCount,
            HeapPageCount = pages.Count,
            Columns = definition.Columns.Select(selector: (column, index) => new StorageColumnStatistics { ColumnId = column.Id, NullCount = nullCounts[index], DistinctValueCount = distinct[index].Count })
            .ToArray(),
            CapturedAt = generations!.ReadStorageGenerationContextTimestamp(storageGenerationContext: new StorageGenerationContext())
            .Timestamp
        };
    }

    private async ValueTask<StorageIndexStatistics> ReadStorageIndexStatisticsAsync(IndexId indexId, CancellationToken cancellationToken = default)
    {
        var definition = catalog.Definition.Indexes.SingleOrDefault(predicate: candidate => candidate.Id == indexId) ?? throw new KeyNotFoundException(message: $"Unknown index {indexId}.");
        var tree = OpenIndex(index: definition);
        long entryCount = 0;
        long distinctKeyCount = 0;
        sql_storage_engine.Models.Indexes.IndexKey? previous = null;

        await foreach (sql_storage_engine.Models.Indexes.LeafIndexEntry entry in ReadCatalogTreeRows(indexTreeBinding: tree, indexRange: new IndexRange { LowerBound = null, UpperBound = null, IncludeLowerBound = true, IncludeUpperBound = true, Direction = ScanDirection.Ascending }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            entryCount++;

            if (previous is null || !previous.Equals(other: entry.Key))
            {
                distinctKeyCount++;
                previous = entry.Key;
            }
        }

        var pages = await ReadCatalogTreePageIdsAsync(indexTreeBinding: tree, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var histogram = definition.Method == CatalogIndexMethod.BTree ? await BuildHistogramAsync(index: definition, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false) : [];

        return new StorageIndexStatistics
        {
            IndexId = indexId,
            EntryCount = entryCount,
            DistinctKeyCount = distinctKeyCount,
            LeafPageCount = pages.Count,
            Histogram = histogram,
            CapturedAt = generations!.ReadStorageGenerationContextTimestamp(storageGenerationContext: new StorageGenerationContext())
            .Timestamp
        };
    }

    private async ValueTask<IReadOnlyList<StorageHistogramBucket>> BuildHistogramAsync(CatalogIndex index, CancellationToken cancellationToken)
    {
        const int maximumBuckets = 200;
        var table = catalog.Definition.Tables.Single(predicate: candidate => candidate.Id == index.TableId);

        var storage = await ReadCatalogTableStorageContextAsync(tableId: table.Id, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var groups = new Dictionary<sql_storage_engine.Models.Indexes.IndexKey, (SqlValue[] Values, long Count)>();

        await foreach (var stored in ScanTableRowsAsync(tableStorageContext: storage, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            var values = index.Columns.Select(selector: indexed => table.Columns.Select(selector: (column, position) => (column, position))
            .Single(predicate: item => item.column.Id == indexed.ColumnId)
            .position)
            .Select(selector: position => stored.Row.Values[position])
            .ToArray();

            sql_storage_engine.Models.Indexes.IndexKey key = catalogIndexKeys.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 10, Values = values, Table = table, Index = index })
            .Key!;

            groups[key] = groups.TryGetValue(key: key, value: out var existing) ? (existing.Values, existing.Count + 1) : (values, 1);
        }

        if (groups.Count == 0)
        {
            return[];
        }

        var ordered = groups.OrderBy(keySelector: group => group.Key.Content, comparer: System.StringComparer.Ordinal)
            .Select(selector: group => group.Value)
            .ToArray();

        var bucketCount = Math.Min(val1: maximumBuckets, val2: ordered.Length);
        List<StorageHistogramBucket> histogram = new(bucketCount);

        for (var bucket = 0; bucket < bucketCount; bucket++)
        {
            var start = bucket * ordered.Length / bucketCount;
            var end = (bucket + 1) * ordered.Length / bucketCount;
            var upper = ordered[end - 1];
            histogram.Add(item: new StorageHistogramBucket { UpperBound = upper.Values, RangeRows = ordered[start..(end - 1)].Sum(selector: group => group.Count), EqualRows = upper.Count, DistinctRangeValues = end - start - 1 });
        }

        return histogram.AsReadOnly();
    }
}