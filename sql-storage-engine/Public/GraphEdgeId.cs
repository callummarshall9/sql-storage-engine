using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;

namespace sql_storage_engine;

/// <summary>A durable, database/table/schema-scoped graph edge identity.</summary>
public readonly record struct GraphEdgeId
{
    private const byte Kind = 2;

    public GraphEdgeId(DatabaseId databaseId, TableId tableId, ulong schemaVersion, Guid value)
    {
        _ = GraphIdentityCodec.Encode(Kind, databaseId, tableId, schemaVersion, value);
        DatabaseId = databaseId;
        TableId = tableId;
        SchemaVersion = schemaVersion;
        Value = value;
    }

    public const int EncodedLength = GraphIdentityCodec.EncodedLength;
    public DatabaseId DatabaseId { get; }
    public TableId TableId { get; }
    public ulong SchemaVersion { get; }
    public Guid Value { get; }
    public SqlValue ToSqlValue() => GraphIdentityCodec.Encode(Kind, DatabaseId, TableId, SchemaVersion, Value);
    public static GraphEdgeId FromSqlValue(SqlValue value)
    {
        var decoded = GraphIdentityCodec.Decode(value, Kind, nameof(value));
        return new GraphEdgeId(decoded.DatabaseId, decoded.TableId, decoded.SchemaVersion, decoded.Value);
    }
    public override string ToString() => $"graph-edge:{TableId.Value}/{Value:D}";
}
