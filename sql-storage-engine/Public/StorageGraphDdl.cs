using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;

namespace sql_storage_engine;

/// <summary>Creates a new graph table inside the caller's durable statement journal.</summary>
internal static class StorageGraphDdl
{
    internal static async ValueTask<CatalogTable> CreateAsync(StorageEngine owner, CatalogTableName name,
        IEnumerable<CatalogColumn> payloadColumns, TableId? from, TableId? to, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(payloadColumns);
        var payload = payloadColumns.ToArray();
        if (payload.Any(column => column is null || column.IsHidden || column.GeneratedAlways != CatalogGeneratedAlwaysKind.None
            || column.Name.StartsWith("$", StringComparison.Ordinal)))
            throw new ArgumentException("Graph payload cannot supply generated or reserved columns.", nameof(payloadColumns));
        var edge = from.HasValue;
        if (edge != to.HasValue) throw new ArgumentException("Both endpoint tables are required.");
        if (edge)
        {
            ValidateNode(owner, from!.Value);
            ValidateNode(owner, to!.Value);
        }
        var last = payload.Length == 0 ? 0UL : payload.Max(column => column.Id.Value);
        var identity = new ColumnId(checked(last + 1));
        var generated = new List<CatalogColumn> { Column(identity, edge ? "$edge_id" : "$node_id", CatalogGeneratedAlwaysKind.GraphIdentity) };
        if (edge)
        {
            generated.Add(Column(new ColumnId(checked(last + 2)), "$from_id", CatalogGeneratedAlwaysKind.GraphFromNode));
            generated.Add(Column(new ColumnId(checked(last + 3)), "$to_id", CatalogGeneratedAlwaysKind.GraphToNode));
        }
        var table = await owner.CreateTableAsync(name, payload.Concat(generated), cancellationToken: token).ConfigureAwait(false);
        owner.ObserveGraphDdl(GraphDdlStage.TableCreated, token);
        var identityIndex = await owner.CreateIndexAsync("$graph_identity", table.Id, true, [Indexed(identity)], token).ConfigureAwait(false);
        owner.ObserveGraphDdl(GraphDdlStage.IdentityIndexCreated, token);
        if (!edge)
        {
            var node = await owner.GraphCatalog.RegisterGraphNodeTableAsync(table.Id, identity, identityIndex.Id, token).ConfigureAwait(false);
            owner.ObserveGraphDdl(GraphDdlStage.Registered, token);
            return node;
        }
        var outgoing = await owner.CreateIndexAsync("$graph_outgoing", table.Id, false, [Indexed(generated[1].Id)], token).ConfigureAwait(false);
        owner.ObserveGraphDdl(GraphDdlStage.OutgoingIndexCreated, token);
        var incoming = await owner.CreateIndexAsync("$graph_incoming", table.Id, false, [Indexed(generated[2].Id)], token).ConfigureAwait(false);
        owner.ObserveGraphDdl(GraphDdlStage.IncomingIndexCreated, token);
        var result = await owner.GraphCatalog.RegisterGraphEdgeTableAsync(table.Id, identity, identityIndex.Id,
            from!.Value, generated[1].Id, outgoing.Id, to!.Value, generated[2].Id, incoming.Id, token).ConfigureAwait(false);
        owner.ObserveGraphDdl(GraphDdlStage.Registered, token);
        return result;
    }

    private static void ValidateNode(StorageEngine owner, TableId id)
    {
        if (!owner.Catalog.TryGetTable(id, out var table) || table!.Graph?.Kind != GraphTableKind.Node)
            throw new ArgumentException("A graph endpoint must reference a registered node table.");
    }
    private static CatalogColumn Column(ColumnId id, string name, CatalogGeneratedAlwaysKind kind) =>
        new(id, name, SqlType.Binary(GraphNodeId.EncodedLength), false, generatedAlways: kind, isHidden: true);
    private static CatalogIndexedColumn Indexed(ColumnId id) => new(id, SortDirection.Ascending, NullSortOrder.First);
}
