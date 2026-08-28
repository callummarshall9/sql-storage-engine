namespace sql_storage_engine;

/// <summary>Selects graph edges by endpoint direction.</summary>
public enum GraphEdgeDirection : byte
{
    Outgoing = 1,
    Incoming = 2,
    Both = 3
}
