using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Storage;

/// <summary>Indicates that legacy indexed vector data produced a non-finite nearest-neighbor distance.</summary>
public sealed class NonFiniteVectorDistanceException : StorageException
{
    public NonFiniteVectorDistanceException(string indexName, RowId rowId)
        : base($"Vector index '{indexName}' produced a non-finite distance for row {rowId}.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
        IndexName = indexName;
        RowId = rowId;
    }

    public string IndexName { get; }

    public RowId RowId { get; }
}
