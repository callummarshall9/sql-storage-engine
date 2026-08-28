namespace sql_storage_engine.Catalog;

/// <summary>Identifies the durable role of a graph table.</summary>
public enum GraphTableKind : byte
{
    Node = 1,
    Edge = 2
}
