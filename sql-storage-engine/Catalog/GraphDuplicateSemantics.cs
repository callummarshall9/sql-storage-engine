namespace sql_storage_engine.Catalog;

/// <summary>The storage-level duplicate contract for directional adjacency.</summary>
public enum GraphDuplicateSemantics : byte
{
    PreserveParallelEdgesAndDeduplicateSelfLoopForBoth = 1
}
