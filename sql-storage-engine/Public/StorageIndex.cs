using sql_storage_engine.Buffers;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Overflow;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;
using sql_storage_engine.Transactions;

namespace sql_storage_engine;

internal sealed class StorageIndex(CatalogIndex definition, CatalogTable table, PersistentBPlusTree tree,
    StorageEngine owner, Func<bool>? isActive = null) : IStorageIndex
{
    private readonly long _generation = owner.HandleGeneration;
    public CatalogIndex Definition => definition;

    public async ValueTask<IReadOnlyList<RowId>> FindAsync(IReadOnlyList<SqlValue> values,
        CancellationToken cancellationToken = default)
    {
        EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken, reentrant: isActive is not null).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureCurrent();
        return await tree.FindAsync(CatalogIndexKey.EncodeValues(values, table, definition), cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<StorageIndexEntry>> FindEntriesAsync(IReadOnlyList<SqlValue> values,
        CancellationToken cancellationToken = default)
    {
        EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken, reentrant: isActive is not null).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureCurrent();
        return (await tree.FindEntriesAsync(CatalogIndexKey.EncodeValues(values, table, definition), cancellationToken)
            .ConfigureAwait(false)).Select(ToStorageEntry).ToArray();
    }

    public IAsyncEnumerable<RowId> ScanAsync(IReadOnlyList<SqlValue> lowerBound,
        IReadOnlyList<SqlValue> upperBound, bool includeLowerBound = true, bool includeUpperBound = true,
        ScanDirection direction = ScanDirection.Ascending,
        CancellationToken cancellationToken = default) =>
        owner.TrackStream(ScanAsyncCore(lowerBound, upperBound, includeLowerBound, includeUpperBound, direction, cancellationToken), isActive is not null);

    private async IAsyncEnumerable<RowId> ScanAsyncCore(IReadOnlyList<SqlValue> lowerBound,
        IReadOnlyList<SqlValue> upperBound, bool includeLowerBound = true, bool includeUpperBound = true,
        ScanDirection direction = ScanDirection.Ascending,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken, reentrant: isActive is not null).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureCurrent();
        var range = new IndexRange(CatalogIndexKey.EncodeValues(lowerBound, table, definition),
            CatalogIndexKey.EncodeValues(upperBound, table, definition), includeLowerBound, includeUpperBound, direction);
        await foreach (var entry in tree.ScanAsync(range, cancellationToken).ConfigureAwait(false))
            yield return entry.RowId;
    }

    public IAsyncEnumerable<RowId> ScanAsync(StorageIndexRange range,
        CancellationToken cancellationToken = default) =>
        owner.TrackStream(ScanAsyncCore(range, cancellationToken), isActive is not null);

    private async IAsyncEnumerable<RowId> ScanAsyncCore(StorageIndexRange range,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken, reentrant: isActive is not null).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureCurrent();
        ArgumentNullException.ThrowIfNull(range);
        if (definition.Method != CatalogIndexMethod.BTree)
            throw new InvalidOperationException("Bounded scans require a B-tree index.");
        var lower = EncodeBound(range.LowerBound, lower: true);
        var upper = EncodeBound(range.UpperBound, lower: false);
        if (lower.BeyondEnd) yield break;
        var physical = new IndexRange(lower.Key, upper.Key, lower.Inclusive, upper.Inclusive, range.Direction);
        await foreach (var entry in tree.ScanAsync(physical, cancellationToken).ConfigureAwait(false))
            yield return entry.RowId;
    }

    public IAsyncEnumerable<StorageIndexEntry> ScanEntriesAsync(StorageIndexRange range,
        CancellationToken cancellationToken = default) =>
        owner.TrackStream(ScanEntriesAsyncCore(range, cancellationToken), isActive is not null);

    private async IAsyncEnumerable<StorageIndexEntry> ScanEntriesAsyncCore(StorageIndexRange range,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken, reentrant: isActive is not null).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureCurrent();
        ArgumentNullException.ThrowIfNull(range);
        var lower = EncodeBound(range.LowerBound, lower: true);
        var upper = EncodeBound(range.UpperBound, lower: false);
        if (lower.BeyondEnd) yield break;
        var physical = new IndexRange(lower.Key, upper.Key, lower.Inclusive, upper.Inclusive, range.Direction);
        await foreach (var entry in tree.ScanAsync(physical, cancellationToken).ConfigureAwait(false))
            yield return ToStorageEntry(entry);
    }

    private StorageIndexEntry ToStorageEntry(LeafIndexEntry entry) => new(entry.RowId,
        CatalogIndexKey.DecodeIncludedValues(entry.IncludedPayload.Span, table, definition));

    private (IndexKey? Key, bool Inclusive, bool BeyondEnd) EncodeBound(StorageIndexBound? bound, bool lower)
    {
        if (bound is null) return (null, true, false);
        ArgumentNullException.ThrowIfNull(bound.Values);
        if (bound.Values.Count == 0 || bound.Values.Count > definition.Columns.Count)
            throw new ArgumentException($"A bound must contain between 1 and {definition.Columns.Count} values.",
                lower ? nameof(StorageIndexRange.LowerBound) : nameof(StorageIndexRange.UpperBound));
        var key = CatalogIndexKey.EncodePrefix(bound.Values, table, definition);
        if (bound.Values.Count == definition.Columns.Count) return (key, bound.Inclusive, false);

        // Every complete key sharing a composite prefix sorts in [prefix, successor(prefix)).
        if (lower && bound.Inclusive || !lower && !bound.Inclusive) return (key, false, false);
        var successor = PrefixSuccessor(key);
        return successor is null ? (null, false, lower) : (successor, false, false);
    }

    private static IndexKey? PrefixSuccessor(IndexKey prefix)
    {
        var bytes = prefix.Bytes.ToArray();
        for (var index = bytes.Length - 1; index >= 0; index--)
        {
            if (bytes[index] == byte.MaxValue) continue;
            bytes[index]++;
            return new IndexKey(bytes.AsSpan(0, index + 1));
        }
        return null;
    }

    public async ValueTask<IReadOnlyList<SpecializedIndexMatch>> SearchNearestAsync(SqlValue query, int count,
        CancellationToken cancellationToken = default)
    {
        EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken, reentrant: isActive is not null).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureCurrent();
        ArgumentNullException.ThrowIfNull(query);
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (definition.Method is not (CatalogIndexMethod.Spatial or CatalogIndexMethod.Vector))
            throw new InvalidOperationException("Nearest-neighbor search requires a spatial or vector index.");
        var columnId = definition.Columns.Single().ColumnId;
        var sourceColumn = table.Columns.Select((column, position) => (column, position))
            .Single(item => item.column.Id == columnId);
        if (query.IsNull) throw new ArgumentException("Nearest-neighbor query values cannot be NULL.", nameof(query));
        sourceColumn.column.Type.Validate(query, "query");
        if (definition.Method == CatalogIndexMethod.Spatial &&
            definition.SpecializedOptions!.SpatialSrid is { } requiredSrid &&
            ((SpatialSqlValue)query).Value.Srid != requiredSrid)
            throw new ArgumentException($"Spatial index '{definition.Name}' requires SRID {requiredSrid}.", nameof(query));
        if (definition.Method == CatalogIndexMethod.Vector &&
            query is VectorSqlValue queryVector &&
            !double.IsFinite(queryVector.DistanceTo(
                queryVector,
                definition.SpecializedOptions!.VectorMetric!.Value)))
        {
            throw new ArgumentException(
                $"Vector index '{definition.Name}' cannot search with a zero vector for cosine distance.",
                nameof(query));
        }
        var columnPosition = sourceColumn.position;
        var storage = await owner.OpenTableStorageAsync(table.Id, cancellationToken).ConfigureAwait(false);
        List<SpecializedIndexMatch> matches = [];
        var range = new IndexRange(new IndexKey([0]), new IndexKey([2]));
        await foreach (var entry in tree.ScanAsync(range, cancellationToken).ConfigureAwait(false))
        {
            var stored = await storage.TryGetAsync(entry.RowId, cancellationToken).ConfigureAwait(false);
            if (!stored.Found || stored.Row!.Values[columnPosition].IsNull) continue;
            var distance = (definition.Method, query, stored.Row.Values[columnPosition]) switch
            {
                (CatalogIndexMethod.Spatial, SpatialSqlValue requested, SpatialSqlValue candidate) =>
                    requested.Value.STDistance(candidate.Value) ?? double.NaN,
                (CatalogIndexMethod.Vector, VectorSqlValue requested, VectorSqlValue candidate) =>
                    requested.DistanceTo(candidate, definition.SpecializedOptions!.VectorMetric!.Value),
                _ => throw new ArgumentException("Query value does not match the specialized index type.", nameof(query))
            };
            if (!double.IsFinite(distance))
            {
                if (definition.Method == CatalogIndexMethod.Vector)
                    throw new NonFiniteVectorDistanceException(definition.Name, entry.RowId);
                continue;
            }
            matches.Add(new SpecializedIndexMatch(entry.RowId, distance));
        }
        return matches.OrderBy(match => match.Distance).ThenBy(match => match.RowId.PageId.Value)
            .ThenBy(match => match.RowId.SlotId.Value).Take(count).ToArray();
    }

    public async ValueTask<IReadOnlyList<RowId>> FindJsonPathAsync(string path, SqlValue value,
        CancellationToken cancellationToken = default)
    {
        EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken, reentrant: isActive is not null).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureCurrent();
        HashSet<RowId> rows = [];
        foreach (var key in CatalogIndexKey.EncodeJsonPathValueKeys(definition, path, value))
            rows.UnionWith(await tree.FindAsync(key, cancellationToken).ConfigureAwait(false));
        return rows.OrderBy(row => row.PageId.Value).ThenBy(row => row.SlotId.Value)
            .ThenBy(row => row.Generation).ToArray();
    }

    public async ValueTask<IReadOnlyList<RowId>> FindJsonPathExistsAsync(string path,
        CancellationToken cancellationToken = default)
    {
        EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken, reentrant: isActive is not null).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureCurrent();
        HashSet<RowId> rows = [];
        foreach (var key in CatalogIndexKey.EncodeJsonPathExistsKeys(definition, path))
            rows.UnionWith(await tree.FindAsync(key, cancellationToken).ConfigureAwait(false));
        return rows.OrderBy(row => row.PageId.Value).ThenBy(row => row.SlotId.Value)
            .ThenBy(row => row.Generation).ToArray();
    }

    public async ValueTask<IReadOnlyList<RowId>> FindXmlPathAsync(string path, string? value = null,
        CancellationToken cancellationToken = default)
    {
        EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken, reentrant: isActive is not null).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureCurrent();
        return await tree.FindAsync(value is null
            ? CatalogIndexKey.EncodeXmlPathExists(definition, path)
            : CatalogIndexKey.EncodeXmlPathValue(definition, path, value), cancellationToken).ConfigureAwait(false);
    }

    public IAsyncEnumerable<FullTextIndexMatch> SearchFullTextAsync(FullTextSearchRequest request,
        CancellationToken cancellationToken = default) =>
        owner.TrackStream(SearchFullTextAsyncCore(request, cancellationToken), isActive is not null);

    private async IAsyncEnumerable<FullTextIndexMatch> SearchFullTextAsyncCore(FullTextSearchRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureCurrent();
        ArgumentNullException.ThrowIfNull(request);
        if (definition.Method != CatalogIndexMethod.FullText)
            throw new InvalidOperationException("Full-text search requires a full-text index.");
        var key = CatalogIndexKey.EncodeFullTextTerm(definition, table, request);
        using var lease = await owner.EnterStatementGateAsync(cancellationToken, reentrant: isActive is not null).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureCurrent();
        var range = new IndexRange(key, key, true, true);
        await foreach (var entry in tree.ScanAsync(range, cancellationToken).ConfigureAwait(false))
            yield return new FullTextIndexMatch(entry.RowId);
    }

    private void EnsureCurrent()
    {
        if (isActive is not null && !isActive()) throw new InvalidOperationException("The statement scope has completed.");
        owner.ThrowIfDisposed();
        if (_generation != owner.HandleGeneration)
            throw new InvalidOperationException("The storage handle is stale after statement rollback; reopen it from the catalog.");
    }
}
