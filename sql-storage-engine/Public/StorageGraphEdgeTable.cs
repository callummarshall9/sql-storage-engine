using System.Runtime.CompilerServices;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;

namespace sql_storage_engine;

internal sealed class StorageGraphEdgeTable(TableStorage storage, StorageEngine owner, long generation,
    Func<bool>? isActive = null)
    : IStorageGraphEdgeTable
{
    private readonly GraphHandleScope _scope = new(owner, generation, isActive);

    public CatalogTable Definition => storage.Definition;
    public CatalogGraphTable GraphDefinition => Definition.Graph!;

    public async ValueTask<GraphEdgeId> InsertAsync(GraphNodeId fromNodeId, GraphNodeId toNodeId, Row row,
        CancellationToken cancellationToken = default)
    {
        _scope.EnsureCurrent();
        GraphStorageRuntime.ValidatePayload(row, Definition, 3, nameof(row));
        using var lease = await _scope.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = owner.ActivateGate(lease);
        await EnsureNodeExistsAsync(fromNodeId, GraphDefinition.FromNodeTableId!.Value, nameof(fromNodeId),
            cancellationToken).ConfigureAwait(false);
        await EnsureNodeExistsAsync(toNodeId, GraphDefinition.ToNodeTableId!.Value, nameof(toNodeId),
            cancellationToken).ConfigureAwait(false);
        var edgeId = new GraphEdgeId(owner.DatabaseId, Definition.Id, Definition.SchemaVersion, Guid.NewGuid());
        var result = await storage.InsertGraphAsync(GraphStorageRuntime.Append(row, edgeId.ToSqlValue(),
                fromNodeId.ToSqlValue(), toNodeId.ToSqlValue()), cancellationToken).ConfigureAwait(false);
        if (!result.Inserted) throw new DuplicateKeyIgnoredException(result.Warnings.Single());
        await _scope.PublishAsync(cancellationToken).ConfigureAwait(false);
        return edgeId;
    }

    public async ValueTask<StoredGraphEdge?> GetAsync(GraphEdgeId edgeId,
        CancellationToken cancellationToken = default)
    {
        _scope.EnsureCurrent();
        GraphStorageRuntime.Validate(edgeId, owner.DatabaseId, Definition, nameof(edgeId));
        using var lease = await _scope.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = owner.ActivateGate(lease);
        var rowId = await FindAsync(edgeId, cancellationToken).ConfigureAwait(false);
        return rowId is null ? null : await ReadAsync(rowId.Value, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> UpdateAsync(GraphEdgeId edgeId, RowUpdate update,
        CancellationToken cancellationToken = default)
    {
        _scope.EnsureCurrent();
        GraphStorageRuntime.Validate(edgeId, owner.DatabaseId, Definition, nameof(edgeId));
        GraphStorageRuntime.ValidatePayloadUpdate(update, Definition.Columns.Count - 3);
        using var lease = await _scope.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = owner.ActivateGate(lease);
        var rowId = await FindAsync(edgeId, cancellationToken).ConfigureAwait(false);
        if (rowId is null) return false;
        var result = await storage.UpdateGraphAsync(rowId.Value, update, cancellationToken).ConfigureAwait(false);
        if (result.Updated) await _scope.PublishAsync(cancellationToken).ConfigureAwait(false);
        return result.Updated;
    }

    public ValueTask<bool> ReconnectAsync(GraphEdgeId edgeId, GraphNodeId fromNodeId,
        GraphNodeId toNodeId, CancellationToken cancellationToken = default)
        => UpdateAndReconnectAsync(edgeId, fromNodeId, toNodeId, new RowUpdate([]), cancellationToken);

    public async ValueTask<bool> UpdateAndReconnectAsync(GraphEdgeId edgeId, GraphNodeId fromNodeId,
        GraphNodeId toNodeId, RowUpdate update, CancellationToken cancellationToken = default)
    {
        _scope.EnsureCurrent();
        GraphStorageRuntime.Validate(edgeId, owner.DatabaseId, Definition, nameof(edgeId));
        GraphStorageRuntime.ValidatePayloadUpdate(update, Definition.Columns.Count - 3);
        using var lease = await _scope.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = owner.ActivateGate(lease);
        await EnsureNodeExistsAsync(fromNodeId, GraphDefinition.FromNodeTableId!.Value, nameof(fromNodeId),
            cancellationToken).ConfigureAwait(false);
        await EnsureNodeExistsAsync(toNodeId, GraphDefinition.ToNodeTableId!.Value, nameof(toNodeId),
            cancellationToken).ConfigureAwait(false);
        var rowId = await FindAsync(edgeId, cancellationToken).ConfigureAwait(false);
        if (rowId is null) return false;
        var result = await storage.UpdateGraphAsync(rowId.Value, new RowUpdate([
            .. update.Columns,
            new ColumnUpdate(Definition.Columns.Count - 2, fromNodeId.ToSqlValue()),
            new ColumnUpdate(Definition.Columns.Count - 1, toNodeId.ToSqlValue())
        ]), cancellationToken).ConfigureAwait(false);
        if (result.Updated) await _scope.PublishAsync(cancellationToken).ConfigureAwait(false);
        return result.Updated;
    }

    public async ValueTask<bool> DeleteAsync(GraphEdgeId edgeId, CancellationToken cancellationToken = default)
    {
        _scope.EnsureCurrent();
        GraphStorageRuntime.Validate(edgeId, owner.DatabaseId, Definition, nameof(edgeId));
        using var lease = await _scope.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = owner.ActivateGate(lease);
        var rowId = await FindAsync(edgeId, cancellationToken).ConfigureAwait(false);
        if (rowId is null) return false;
        var result = await storage.DeleteAsync(rowId.Value, cancellationToken).ConfigureAwait(false);
        if (result.Deleted) await _scope.PublishAsync(cancellationToken).ConfigureAwait(false);
        return result.Deleted;
    }

    public IAsyncEnumerable<StoredGraphEdge> TraverseAsync(GraphNodeId nodeId, GraphEdgeDirection direction,
        GraphTraversalOptions? options = null,
        CancellationToken cancellationToken = default) =>
        owner.TrackStream(TraverseAsyncCore(nodeId, direction, options, cancellationToken), isActive is not null);

    private async IAsyncEnumerable<StoredGraphEdge> TraverseAsyncCore(GraphNodeId nodeId, GraphEdgeDirection direction,
        GraphTraversalOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _scope.EnsureCurrent();
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        options ??= new GraphTraversalOptions();
        using var lease = await _scope.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = owner.ActivateGate(lease);
        var outgoing = GraphDefinition.FromNodeTableId == nodeId.TableId;
        var incoming = GraphDefinition.ToNodeTableId == nodeId.TableId;
        if (direction == GraphEdgeDirection.Outgoing && !outgoing ||
            direction == GraphEdgeDirection.Incoming && !incoming ||
            direction == GraphEdgeDirection.Both && !outgoing && !incoming)
            throw new ArgumentException("The graph node identity is incompatible with the requested edge direction.",
                nameof(nodeId));

        var nodeValidated = false;
        if (outgoing && (direction is GraphEdgeDirection.Outgoing or GraphEdgeDirection.Both))
        {
            await EnsureNodeExistsAsync(nodeId, GraphDefinition.FromNodeTableId!.Value, nameof(nodeId),
                cancellationToken).ConfigureAwait(false);
            nodeValidated = true;
        }
        if (incoming && (direction is GraphEdgeDirection.Incoming or GraphEdgeDirection.Both) && !nodeValidated)
            await EnsureNodeExistsAsync(nodeId, GraphDefinition.ToNodeTableId!.Value, nameof(nodeId),
                cancellationToken).ConfigureAwait(false);

        HashSet<RowId> rows = [];
        if ((direction is GraphEdgeDirection.Outgoing or GraphEdgeDirection.Both) && outgoing)
            await AddMatchesAsync(GraphDefinition.OutgoingIndexId!.Value, nodeId, rows, options.MaximumEdges,
                cancellationToken).ConfigureAwait(false);
        if ((direction is GraphEdgeDirection.Incoming or GraphEdgeDirection.Both) && incoming)
            await AddMatchesAsync(GraphDefinition.IncomingIndexId!.Value, nodeId, rows, options.MaximumEdges,
                cancellationToken).ConfigureAwait(false);

        foreach (var rowId in rows.OrderBy(row => row, Comparer<RowId>.Create(GraphStorageRuntime.Compare)))
        {
            _scope.EnsureCurrent();
            cancellationToken.ThrowIfCancellationRequested();
            yield return await ReadAsync(rowId, cancellationToken).ConfigureAwait(false);
        }
    }

    private ValueTask<RowId?> FindAsync(GraphEdgeId edgeId, CancellationToken cancellationToken) =>
        GraphStorageRuntime.FindUniqueAsync(owner.GraphCatalog, Definition, GraphDefinition.IdentityIndexId,
            edgeId.ToSqlValue(), cancellationToken);

    private async ValueTask EnsureNodeExistsAsync(GraphNodeId nodeId, TableId expectedTableId, string parameterName,
        CancellationToken cancellationToken)
    {
        var table = owner.GraphCatalog.Tables.Single(table => table.Id == expectedTableId);
        GraphStorageRuntime.Validate(nodeId, owner.DatabaseId, table, parameterName);
        var rowId = await GraphStorageRuntime.FindUniqueAsync(owner.GraphCatalog, table,
            table.Graph!.IdentityIndexId, nodeId.ToSqlValue(), cancellationToken).ConfigureAwait(false);
        if (rowId is null)
            throw new GraphReferentialIntegrityException("A graph edge endpoint does not reference a live node.");
    }

    private async ValueTask AddMatchesAsync(IndexId indexId, GraphNodeId nodeId, HashSet<RowId> rows,
        int maximumEdges, CancellationToken cancellationToken)
    {
        var index = owner.GraphCatalog.Indexes.Single(candidate => candidate.Id == indexId);
        var range = GraphStorageRuntime.ExactRange(Definition, index, nodeId.ToSqlValue());
        await foreach (var entry in owner.GraphCatalog.OpenIndex(index).ScanAsync(range, cancellationToken)
                           .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (rows.Contains(entry.RowId)) continue;
            if (rows.Count == maximumEdges)
                throw new StorageResourceExhaustedException(
                    $"Graph adjacency exceeds the configured {maximumEdges}-edge traversal limit.");
            rows.Add(entry.RowId);
        }
    }

    private async ValueTask<StoredGraphEdge> ReadAsync(RowId rowId, CancellationToken cancellationToken)
    {
        var found = await storage.TryGetAsync(rowId, cancellationToken).ConfigureAwait(false);
        if (!found.Found) throw new StorageCorruptionException("A graph index references a missing edge row.");
        var row = found.Row!;
        try
        {
            var edgeId = GraphEdgeId.FromSqlValue(row.Values[^3]);
            var fromNodeId = GraphNodeId.FromSqlValue(row.Values[^2]);
            var toNodeId = GraphNodeId.FromSqlValue(row.Values[^1]);
            GraphStorageRuntime.Validate(edgeId, owner.DatabaseId, Definition, nameof(edgeId));
            GraphStorageRuntime.Validate(fromNodeId, owner.DatabaseId,
                owner.GraphCatalog.Tables.Single(table => table.Id == GraphDefinition.FromNodeTableId),
                nameof(fromNodeId));
            GraphStorageRuntime.Validate(toNodeId, owner.DatabaseId,
                owner.GraphCatalog.Tables.Single(table => table.Id == GraphDefinition.ToNodeTableId),
                nameof(toNodeId));
            return new StoredGraphEdge(edgeId, fromNodeId, toNodeId, GraphStorageRuntime.Payload(row, 3));
        }
        catch (ArgumentException exception)
        {
            throw new StorageCorruptionException(
                "A stored graph edge identity has invalid ownership metadata.", exception);
        }
    }
}
