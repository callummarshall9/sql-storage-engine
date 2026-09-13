using System.Runtime.CompilerServices;
using sql_storage_engine.Catalog;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;

namespace sql_storage_engine;

internal sealed class StorageGraphNodeTable(TableStorage storage, StorageEngine owner, long generation,
    Func<bool>? isActive = null)
    : IStorageGraphNodeTable
{
    private readonly GraphHandleScope _scope = new(owner, generation, isActive);

    public CatalogTable Definition => storage.Definition;
    public CatalogGraphTable GraphDefinition => Definition.Graph!;

    public async ValueTask<GraphNodeId> InsertAsync(Row row, CancellationToken cancellationToken = default)
    {
        _scope.EnsureCurrent();
        GraphStorageRuntime.ValidatePayload(row, Definition, 1, nameof(row));
        using var lease = await _scope.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = owner.ActivateGate(lease);
        var nodeId = new GraphNodeId(owner.DatabaseId, Definition.Id, Definition.SchemaVersion, Guid.NewGuid());
        var result = await storage.InsertGraphAsync(GraphStorageRuntime.Append(row, nodeId.ToSqlValue()), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Inserted) throw new DuplicateKeyIgnoredException(result.Warnings.Single());
        await _scope.PublishAsync(cancellationToken).ConfigureAwait(false);
        return nodeId;
    }

    public async ValueTask<StoredGraphNode?> GetAsync(GraphNodeId nodeId,
        CancellationToken cancellationToken = default)
    {
        _scope.EnsureCurrent();
        GraphStorageRuntime.Validate(nodeId, owner.DatabaseId, Definition, nameof(nodeId));
        using var lease = await _scope.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = owner.ActivateGate(lease);
        var rowId = await FindAsync(nodeId, cancellationToken).ConfigureAwait(false);
        if (rowId is null) return null;
        var found = await storage.TryGetAsync(rowId.Value, cancellationToken).ConfigureAwait(false);
        if (!found.Found) throw new StorageCorruptionException("A graph identity index references a missing node row.");
        return ToNode(found.Row!);
    }

    public IAsyncEnumerable<StoredGraphNode> ScanAsync(
        CancellationToken cancellationToken = default) =>
        owner.TrackStream(ScanAsyncCore(cancellationToken), isActive is not null);

    private async IAsyncEnumerable<StoredGraphNode> ScanAsyncCore(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _scope.EnsureCurrent();
        using var lease = await _scope.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = owner.ActivateGate(lease);
        await foreach (var row in storage.ScanAsync(cancellationToken).ConfigureAwait(false))
        {
            _scope.EnsureCurrent();
            cancellationToken.ThrowIfCancellationRequested();
            yield return ToNode(row.Row);
        }
    }

    public async ValueTask<bool> UpdateAsync(GraphNodeId nodeId, RowUpdate update,
        CancellationToken cancellationToken = default)
    {
        _scope.EnsureCurrent();
        GraphStorageRuntime.Validate(nodeId, owner.DatabaseId, Definition, nameof(nodeId));
        GraphStorageRuntime.ValidatePayloadUpdate(update, Definition.Columns.Count - 1);
        using var lease = await _scope.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = owner.ActivateGate(lease);
        var rowId = await FindAsync(nodeId, cancellationToken).ConfigureAwait(false);
        if (rowId is null) return false;
        var result = await storage.UpdateGraphAsync(rowId.Value, update, cancellationToken).ConfigureAwait(false);
        if (result.Updated) await _scope.PublishAsync(cancellationToken).ConfigureAwait(false);
        return result.Updated;
    }

    public async ValueTask<bool> DeleteAsync(GraphNodeId nodeId, CancellationToken cancellationToken = default)
    {
        _scope.EnsureCurrent();
        GraphStorageRuntime.Validate(nodeId, owner.DatabaseId, Definition, nameof(nodeId));
        using var lease = await _scope.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = owner.ActivateGate(lease);
        var rowId = await FindAsync(nodeId, cancellationToken).ConfigureAwait(false);
        if (rowId is null) return false;
        await EnsureUnreferencedAsync(nodeId, cancellationToken).ConfigureAwait(false);
        var result = await storage.DeleteAsync(rowId.Value, cancellationToken).ConfigureAwait(false);
        if (result.Deleted) await _scope.PublishAsync(cancellationToken).ConfigureAwait(false);
        return result.Deleted;
    }

    private ValueTask<Identifiers.RowId?> FindAsync(GraphNodeId nodeId, CancellationToken cancellationToken) =>
        GraphStorageRuntime.FindUniqueAsync(owner.GraphCatalog, Definition, GraphDefinition.IdentityIndexId,
            nodeId.ToSqlValue(), cancellationToken);

    private async ValueTask EnsureUnreferencedAsync(GraphNodeId nodeId, CancellationToken cancellationToken)
    {
        foreach (var edgeTable in owner.GraphCatalog.Tables.Where(table => table.Graph?.Kind == GraphTableKind.Edge))
        {
            var graph = edgeTable.Graph!;
            foreach (var indexId in EndpointIndexes(graph, nodeId.TableId))
            {
                var index = owner.GraphCatalog.Indexes.Single(candidate => candidate.Id == indexId);
                var range = GraphStorageRuntime.ExactRange(edgeTable, index, nodeId.ToSqlValue());
                await using var rows = owner.GraphCatalog.OpenIndex(index).ScanAsync(range, cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);
                if (await rows.MoveNextAsync().ConfigureAwait(false))
                    throw new GraphReferentialIntegrityException(
                        "A graph node cannot be deleted while one or more edges reference it.");
            }
        }
    }

    private static IEnumerable<Identifiers.IndexId> EndpointIndexes(CatalogGraphTable graph,
        Identifiers.TableId nodeTableId)
    {
        if (graph.FromNodeTableId == nodeTableId) yield return graph.OutgoingIndexId!.Value;
        if (graph.ToNodeTableId == nodeTableId && graph.IncomingIndexId != graph.OutgoingIndexId)
            yield return graph.IncomingIndexId!.Value;
    }

    private StoredGraphNode ToNode(Row row)
    {
        try
        {
            var nodeId = GraphNodeId.FromSqlValue(row.Values[^1]);
            GraphStorageRuntime.Validate(nodeId, owner.DatabaseId, Definition, nameof(nodeId));
            return new StoredGraphNode(nodeId, GraphStorageRuntime.Payload(row, 1));
        }
        catch (ArgumentException exception)
        {
            throw new StorageCorruptionException(
                "A stored graph node identity has invalid ownership metadata.", exception);
        }
    }
}
