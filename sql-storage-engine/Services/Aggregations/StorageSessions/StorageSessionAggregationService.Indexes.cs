// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private async ValueTask<IReadOnlyList<RowId>> IndexFindAsync(StorageIndexContext storageIndexContext, IReadOnlyList<SqlValue> values, CancellationToken cancellationToken = default)
    {
        var definition = storageIndexContext.Index!.Definition!;
        var table = storageIndexContext.Table!;
        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);

        var gate = await EnterStorageIndexScopeAsync(storageIndexContext: storageIndexContext, cancellationToken: cancellationToken, reentrant: storageIndexContext.Handle!.Statement is not null)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lease = gate.Lifetime;

        using var accessScope = ActivateStorageIndexScope(storageIndexContext: storageIndexContext, engineResourceContext: gate)
            .Lifetime;

        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);

        return await ReadPublicIndexRowsAsync(storageIndexContext: storageIndexContext, indexKey: PublicIndexKeyEncodeValues(values: values, catalogTable: table, catalogIndex: definition), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private async ValueTask<IReadOnlyList<StorageIndexEntry>> IndexFindEntriesAsync(StorageIndexContext storageIndexContext, IReadOnlyList<SqlValue> values, CancellationToken cancellationToken = default)
    {
        var definition = storageIndexContext.Index!.Definition!;
        var table = storageIndexContext.Table!;
        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);

        var gate = await EnterStorageIndexScopeAsync(storageIndexContext: storageIndexContext, cancellationToken: cancellationToken, reentrant: storageIndexContext.Handle!.Statement is not null)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lease = gate.Lifetime;

        using var accessScope = ActivateStorageIndexScope(storageIndexContext: storageIndexContext, engineResourceContext: gate)
            .Lifetime;

        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);

        return (await ReadPublicIndexEntriesAsync(storageIndexContext: storageIndexContext, indexKey: PublicIndexKeyEncodeValues(values: values, catalogTable: table, catalogIndex: definition), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Select(selector: entry => IndexToStorageEntry(storageIndexContext: storageIndexContext, entry: entry))
            .ToArray();
    }

    private async IAsyncEnumerable<RowId> IndexScanAsyncCore(StorageIndexContext storageIndexContext, IReadOnlyList<SqlValue> lowerBound, IReadOnlyList<SqlValue> upperBound, bool includeLowerBound = true, bool includeUpperBound = true, ScanDirection direction = ScanDirection.Ascending, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var definition = storageIndexContext.Index!.Definition!;
        var table = storageIndexContext.Table!;
        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);

        var gate = await EnterStorageIndexScopeAsync(storageIndexContext: storageIndexContext, cancellationToken: cancellationToken, reentrant: storageIndexContext.Handle!.Statement is not null)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lease = gate.Lifetime;

        using var accessScope = ActivateStorageIndexScope(storageIndexContext: storageIndexContext, engineResourceContext: gate)
            .Lifetime;

        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);

        var range = new IndexRange
        {
            LowerBound = PublicIndexKeyEncodeValues(values: lowerBound, catalogTable: table, catalogIndex: definition),
            UpperBound = PublicIndexKeyEncodeValues(values: upperBound, catalogTable: table, catalogIndex: definition),
            IncludeLowerBound = includeLowerBound,
            IncludeUpperBound = includeUpperBound,
            Direction = direction
        };

        await foreach (sql_storage_engine.Models.Indexes.LeafIndexEntry entry in ScanPublicIndexEntries(storageIndexContext: storageIndexContext, indexRange: range, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            yield return entry.RowId;
        }
    }

    private async IAsyncEnumerable<RowId> IndexScanAsyncCore(StorageIndexContext storageIndexContext, StorageIndexRange range, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var definition = storageIndexContext.Index!.Definition!;
        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);

        var gate = await EnterStorageIndexScopeAsync(storageIndexContext: storageIndexContext, cancellationToken: cancellationToken, reentrant: storageIndexContext.Handle!.Statement is not null)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lease = gate.Lifetime;

        using var accessScope = ActivateStorageIndexScope(storageIndexContext: storageIndexContext, engineResourceContext: gate)
            .Lifetime;

        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);
        ArgumentNullException.ThrowIfNull(argument: range);

        if (definition.Method != CatalogIndexMethod.BTree)
        {
            throw new InvalidOperationException("Bounded scans require a B-tree index.");
        }

        var lower = IndexEncodeBound(storageIndexContext: storageIndexContext, bound: range.LowerBound, lower: true);
        var upper = IndexEncodeBound(storageIndexContext: storageIndexContext, bound: range.UpperBound, lower: false);

        if (lower.BeyondEnd)
        {
            yield break;
        }

        var physical = new IndexRange
        {
            LowerBound = lower.Key,
            UpperBound = upper.Key,
            IncludeLowerBound = lower.Inclusive,
            IncludeUpperBound = upper.Inclusive,
            Direction = range.Direction
        };

        await foreach (sql_storage_engine.Models.Indexes.LeafIndexEntry entry in ScanPublicIndexEntries(storageIndexContext: storageIndexContext, indexRange: physical, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            yield return entry.RowId;
        }
    }

    private async IAsyncEnumerable<StorageIndexEntry> IndexScanEntriesAsyncCore(StorageIndexContext storageIndexContext, StorageIndexRange range, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);

        var gate = await EnterStorageIndexScopeAsync(storageIndexContext: storageIndexContext, cancellationToken: cancellationToken, reentrant: storageIndexContext.Handle!.Statement is not null)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lease = gate.Lifetime;

        using var accessScope = ActivateStorageIndexScope(storageIndexContext: storageIndexContext, engineResourceContext: gate)
            .Lifetime;

        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);
        ArgumentNullException.ThrowIfNull(argument: range);
        var lower = IndexEncodeBound(storageIndexContext: storageIndexContext, bound: range.LowerBound, lower: true);
        var upper = IndexEncodeBound(storageIndexContext: storageIndexContext, bound: range.UpperBound, lower: false);

        if (lower.BeyondEnd)
        {
            yield break;
        }

        var physical = new IndexRange
        {
            LowerBound = lower.Key,
            UpperBound = upper.Key,
            IncludeLowerBound = lower.Inclusive,
            IncludeUpperBound = upper.Inclusive,
            Direction = range.Direction
        };

        await foreach (sql_storage_engine.Models.Indexes.LeafIndexEntry entry in ScanPublicIndexEntries(storageIndexContext: storageIndexContext, indexRange: physical, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            yield return IndexToStorageEntry(storageIndexContext: storageIndexContext, entry: entry);
        }
    }

    private StorageIndexEntry IndexToStorageEntry(StorageIndexContext storageIndexContext, sql_storage_engine.Models.Indexes.LeafIndexEntry entry)
    {
        var definition = storageIndexContext.Index!.Definition!;
        var table = storageIndexContext.Table!;

        return CreatePublicStorageIndexEntry(rowId: entry.RowId, includedValues: PublicIndexKeyDecodeIncludedValues(payload: ReadPublicIndexEntryPayload(leafIndexEntry: entry)
            .Span, catalogTable: table, catalogIndex: definition));
    }

    private (sql_storage_engine.Models.Indexes.IndexKey? Key, bool Inclusive, bool BeyondEnd) IndexEncodeBound(StorageIndexContext storageIndexContext, StorageIndexBound? bound, bool lower)
    {
        var definition = storageIndexContext.Index!.Definition!;
        var table = storageIndexContext.Table!;

        if (bound is null)
        {
            return (null, true, false);
        }

        ArgumentNullException.ThrowIfNull(argument: bound.Values);

        if (bound.Values.Count == 0 || bound.Values.Count > definition.Columns.Count)
        {
            throw new ArgumentException($"A bound must contain between 1 and {definition.Columns.Count} values.", lower ? nameof(StorageIndexRange.LowerBound) : nameof(StorageIndexRange.UpperBound));
        }

        sql_storage_engine.Models.Indexes.IndexKey key = PublicIndexKeyEncodePrefix(values: bound.Values, catalogTable: table, catalogIndex: definition);

        if (bound.Values.Count == definition.Columns.Count)
        {
            return (key, bound.Inclusive, false);
        }

        if (lower && bound.Inclusive || !lower && !bound.Inclusive)
        {
            return (key, false, false);
        }

        sql_storage_engine.Models.Indexes.IndexKey? successor = IndexPrefixSuccessor(storageIndexContext: storageIndexContext, prefix: key);
        return successor is null ? (null, false, lower) : (successor, false, false);
    }

    private static sql_storage_engine.Models.Indexes.IndexKey? IndexPrefixSuccessor(StorageIndexContext storageIndexContext, sql_storage_engine.Models.Indexes.IndexKey prefix)
    {
        var bytes = ReadPublicIndexKeyBytes(indexKey: prefix)
            .ToArray();

        for (var index = bytes.Length - 1; index >= 0; index--)
        {
            if (bytes[index] == byte.MaxValue)
            {
                continue;
            }

            bytes[index]++;
            return CreatePublicIndexKey(bytes: bytes.AsSpan(start: 0, length: index + 1));
        }

        return null;
    }

    private async ValueTask<IReadOnlyList<SpecializedIndexMatch>> IndexSearchNearestAsync(StorageIndexContext storageIndexContext, SqlValue query, int count, CancellationToken cancellationToken = default)
    {
        var definition = storageIndexContext.Index!.Definition!;
        var table = storageIndexContext.Table!;
        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);

        var gate = await EnterStorageIndexScopeAsync(storageIndexContext: storageIndexContext, cancellationToken: cancellationToken, reentrant: storageIndexContext.Handle!.Statement is not null)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lease = gate.Lifetime;

        using var accessScope = ActivateStorageIndexScope(storageIndexContext: storageIndexContext, engineResourceContext: gate)
            .Lifetime;

        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);
        ArgumentNullException.ThrowIfNull(argument: query);

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (definition.Method is not (CatalogIndexMethod.Spatial or CatalogIndexMethod.Vector))
        {
            throw new InvalidOperationException("Nearest-neighbor search requires a spatial or vector index.");
        }

        var columnId = definition.Columns.Single()
            .ColumnId;

        var sourceColumn = table.Columns.Select(selector: (column, position) => (column, position))
            .Single(predicate: item => item.column.Id == columnId);

        if (query.IsNull)
        {
            throw new ArgumentException("Nearest-neighbor query values cannot be NULL.", nameof(query));
        }

        ValidatePublicIndexValue(sqlType: sourceColumn.column.Type, sqlValue: query, columnName: "query");

        if (definition.Method == CatalogIndexMethod.Spatial && definition.SpecializedOptions!.SpatialSrid is { } requiredSrid && ((SpatialSqlValue)query).Value.Srid != requiredSrid)
        {
            throw new ArgumentException($"Spatial index '{definition.Name}' requires SRID {requiredSrid}.", nameof(query));
        }

        if (definition.Method == CatalogIndexMethod.Vector && query is VectorSqlValue queryVector && !double.IsFinite(d: MeasurePublicIndexVectorDistance(vectorSqlValues: [queryVector, queryVector], metric: definition.SpecializedOptions!.VectorMetric!.Value)))
        {
            throw new ArgumentException($"Vector index '{definition.Name}' cannot search with a zero vector for cosine distance.", nameof(query));
        }

        var columnPosition = sourceColumn.position;

        var storage = await ReadCatalogTableStorageContextAsync(tableId: table.Id, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        List<SpecializedIndexMatch> matches = [];

        var range = new IndexRange
        {
            LowerBound = CreatePublicIndexKey(bytes: [0]),
            UpperBound = CreatePublicIndexKey(bytes: [2]),
            IncludeLowerBound = true,
            IncludeUpperBound = true,
            Direction = ScanDirection.Ascending
        };

        await foreach (sql_storage_engine.Models.Indexes.LeafIndexEntry entry in ScanPublicIndexEntries(storageIndexContext: storageIndexContext, indexRange: range, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            var stored = await ReadPublicTableRowAsync(tableStorageContext: storage, rowId: entry.RowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (!stored.Found || stored.Row!.Values[columnPosition].IsNull)
            {
                continue;
            }

            var distance = (definition.Method, query, stored.Row.Values[columnPosition]) switch
            {
                (CatalogIndexMethod.Spatial, SpatialSqlValue requested, SpatialSqlValue candidate) => MeasurePublicIndexSpatialDistance(sqlValues: [requested, candidate]),
                (CatalogIndexMethod.Vector, VectorSqlValue requested, VectorSqlValue candidate) => MeasurePublicIndexVectorDistance(vectorSqlValues: [requested, candidate], metric: definition.SpecializedOptions!.VectorMetric!.Value),
                _ => throw new ArgumentException("Query value does not match the specialized index type.", nameof(query))};

            if (!double.IsFinite(d: distance))
            {
                if (definition.Method == CatalogIndexMethod.Vector)
                {
                    throw new NonFiniteVectorDistanceException(definition.Name, entry.RowId);
                }

                continue;
            }

            matches.Add(item: new SpecializedIndexMatch { RowId = entry.RowId, Distance = distance });
        }

        return matches.OrderBy(keySelector: match => match.Distance)
            .ThenBy(keySelector: match => match.RowId.PageId.Value)
            .ThenBy(keySelector: match => match.RowId.SlotId.Value)
            .Take(count: count)
            .ToArray();
    }

    private async ValueTask<IReadOnlyList<RowId>> IndexFindJsonPathAsync(StorageIndexContext storageIndexContext, string path, SqlValue value, CancellationToken cancellationToken = default)
    {
        var definition = storageIndexContext.Index!.Definition!;
        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);

        var gate = await EnterStorageIndexScopeAsync(storageIndexContext: storageIndexContext, cancellationToken: cancellationToken, reentrant: storageIndexContext.Handle!.Statement is not null)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lease = gate.Lifetime;

        using var accessScope = ActivateStorageIndexScope(storageIndexContext: storageIndexContext, engineResourceContext: gate)
            .Lifetime;

        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);
        HashSet<RowId> rows = [];

        foreach (sql_storage_engine.Models.Indexes.IndexKey key in PublicJsonPathValueIndexKeys(catalogIndex: definition, path: path, sqlValue: value))
        {
            rows.UnionWith(other: await ReadPublicIndexRowsAsync(storageIndexContext: storageIndexContext, indexKey: key, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));
        }

        return rows.OrderBy(keySelector: row => row.PageId.Value)
            .ThenBy(keySelector: row => row.SlotId.Value)
            .ThenBy(keySelector: row => row.Generation)
            .ToArray();
    }

    private async ValueTask<IReadOnlyList<RowId>> IndexFindJsonPathExistsAsync(StorageIndexContext storageIndexContext, string path, CancellationToken cancellationToken = default)
    {
        var definition = storageIndexContext.Index!.Definition!;
        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);

        var gate = await EnterStorageIndexScopeAsync(storageIndexContext: storageIndexContext, cancellationToken: cancellationToken, reentrant: storageIndexContext.Handle!.Statement is not null)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lease = gate.Lifetime;

        using var accessScope = ActivateStorageIndexScope(storageIndexContext: storageIndexContext, engineResourceContext: gate)
            .Lifetime;

        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);
        HashSet<RowId> rows = [];

        foreach (sql_storage_engine.Models.Indexes.IndexKey key in PublicJsonPathExistsIndexKeys(catalogIndex: definition, path: path))
        {
            rows.UnionWith(other: await ReadPublicIndexRowsAsync(storageIndexContext: storageIndexContext, indexKey: key, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));
        }

        return rows.OrderBy(keySelector: row => row.PageId.Value)
            .ThenBy(keySelector: row => row.SlotId.Value)
            .ThenBy(keySelector: row => row.Generation)
            .ToArray();
    }

    private async ValueTask<IReadOnlyList<RowId>> IndexFindXmlPathAsync(StorageIndexContext storageIndexContext, string path, string? value = null, CancellationToken cancellationToken = default)
    {
        var definition = storageIndexContext.Index!.Definition!;
        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);

        var gate = await EnterStorageIndexScopeAsync(storageIndexContext: storageIndexContext, cancellationToken: cancellationToken, reentrant: storageIndexContext.Handle!.Statement is not null)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lease = gate.Lifetime;

        using var accessScope = ActivateStorageIndexScope(storageIndexContext: storageIndexContext, engineResourceContext: gate)
            .Lifetime;

        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);

        return await ReadPublicIndexRowsAsync(storageIndexContext: storageIndexContext, indexKey: value is null ? PublicIndexKeyEncodeXmlPathExists(catalogIndex: definition, path: path) : PublicIndexKeyEncodeXmlPathValue(catalogIndex: definition, path: path, value: value), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private async IAsyncEnumerable<FullTextIndexMatch> IndexSearchFullTextAsyncCore(StorageIndexContext storageIndexContext, FullTextSearchRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var definition = storageIndexContext.Index!.Definition!;
        var table = storageIndexContext.Table!;
        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);
        ArgumentNullException.ThrowIfNull(argument: request);

        if (definition.Method != CatalogIndexMethod.FullText)
        {
            throw new InvalidOperationException("Full-text search requires a full-text index.");
        }

        sql_storage_engine.Models.Indexes.IndexKey key = PublicIndexKeyEncodeFullTextTerm(catalogIndex: definition, catalogTable: table, request: request);

        var gate = await EnterStorageIndexScopeAsync(storageIndexContext: storageIndexContext, cancellationToken: cancellationToken, reentrant: storageIndexContext.Handle!.Statement is not null)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lease = gate.Lifetime;

        using var accessScope = ActivateStorageIndexScope(storageIndexContext: storageIndexContext, engineResourceContext: gate)
            .Lifetime;

        EnsureStorageIndexCurrent(storageIndexContext: storageIndexContext);

        var range = new IndexRange
        {
            LowerBound = key,
            UpperBound = key,
            IncludeLowerBound = true,
            IncludeUpperBound = true,
            Direction = ScanDirection.Ascending
        };

        await foreach (sql_storage_engine.Models.Indexes.LeafIndexEntry entry in ScanPublicIndexEntries(storageIndexContext: storageIndexContext, indexRange: range, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            yield return new FullTextIndexMatch
            {
                RowId = entry.RowId
            };
        }
    }
}