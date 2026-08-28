using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Catalog;

/// <summary>Durable graph identity and adjacency metadata attached to one catalog table.</summary>
public sealed record CatalogGraphTable
{
    private CatalogGraphTable(GraphTableKind kind, ColumnId identityColumnId, IndexId identityIndexId,
        TableId? fromNodeTableId = null, TableId? toNodeTableId = null,
        ColumnId? fromNodeColumnId = null, ColumnId? toNodeColumnId = null,
        IndexId? outgoingIndexId = null, IndexId? incomingIndexId = null)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
        IdentityColumnId = identityColumnId;
        IdentityIndexId = identityIndexId;
        FromNodeTableId = fromNodeTableId;
        ToNodeTableId = toNodeTableId;
        FromNodeColumnId = fromNodeColumnId;
        ToNodeColumnId = toNodeColumnId;
        OutgoingIndexId = outgoingIndexId;
        IncomingIndexId = incomingIndexId;
        if (kind == GraphTableKind.Node && new object?[] { fromNodeTableId, toNodeTableId, fromNodeColumnId,
                toNodeColumnId, outgoingIndexId, incomingIndexId }.Any(value => value is not null))
            throw new ArgumentException("A graph node table cannot declare edge endpoint metadata.");
        if (kind == GraphTableKind.Edge && new object?[] { fromNodeTableId, toNodeTableId, fromNodeColumnId,
                toNodeColumnId, outgoingIndexId, incomingIndexId }.Any(value => value is null))
            throw new ArgumentException("A graph edge table requires complete endpoint and adjacency metadata.");
        if (kind == GraphTableKind.Edge && (identityColumnId == fromNodeColumnId ||
            identityColumnId == toNodeColumnId || fromNodeColumnId == toNodeColumnId))
            throw new ArgumentException("Graph identity and endpoint columns must be distinct.");
        if (kind == GraphTableKind.Edge && (identityIndexId == outgoingIndexId ||
            identityIndexId == incomingIndexId || outgoingIndexId == incomingIndexId))
            throw new ArgumentException("Graph identity and adjacency indexes must be distinct.");
    }

    public GraphTableKind Kind { get; }
    public ColumnId IdentityColumnId { get; }
    public IndexId IdentityIndexId { get; }
    public TableId? FromNodeTableId { get; }
    public TableId? ToNodeTableId { get; }
    public ColumnId? FromNodeColumnId { get; }
    public ColumnId? ToNodeColumnId { get; }
    public IndexId? OutgoingIndexId { get; }
    public IndexId? IncomingIndexId { get; }
    public bool SupportsOutgoingTraversal => Kind == GraphTableKind.Edge;
    public bool SupportsIncomingTraversal => Kind == GraphTableKind.Edge;
    public GraphTraversalOrder TraversalOrder => GraphTraversalOrder.EdgePhysicalIdentity;
    public GraphDuplicateSemantics DuplicateSemantics =>
        GraphDuplicateSemantics.PreserveParallelEdgesAndDeduplicateSelfLoopForBoth;
    public int MaximumEdgesPerTraversal => GraphTraversalOptions.MaximumSupportedEdges;

    public static CatalogGraphTable Node(ColumnId identityColumnId, IndexId identityIndexId) =>
        new(GraphTableKind.Node, identityColumnId, identityIndexId);

    public static CatalogGraphTable Edge(ColumnId identityColumnId, IndexId identityIndexId,
        TableId fromNodeTableId, ColumnId fromNodeColumnId, IndexId outgoingIndexId,
        TableId toNodeTableId, ColumnId toNodeColumnId, IndexId incomingIndexId) =>
        new(GraphTableKind.Edge, identityColumnId, identityIndexId, fromNodeTableId, toNodeTableId,
            fromNodeColumnId, toNodeColumnId, outgoingIndexId, incomingIndexId);
}
