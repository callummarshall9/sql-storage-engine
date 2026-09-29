namespace sql_storage_engine.Storage;

/// <summary>Reports every index page allocated by a failed build and any page that cleanup could not reclaim.</summary>
public sealed class IndexBuildException : StorageException
{
    public IndexBuildException(string message, IReadOnlyList<sql_storage_engine.Identifiers.PageId> allocatedPageIds,
        IReadOnlyList<sql_storage_engine.Identifiers.PageId> unreclaimedPageIds, Exception innerException)
        : base(message, innerException)
    {
        AllocatedPageIds = allocatedPageIds.ToArray();
        UnreclaimedPageIds = unreclaimedPageIds.ToArray();
    }
    public IReadOnlyList<sql_storage_engine.Identifiers.PageId> AllocatedPageIds { get; }
    public IReadOnlyList<sql_storage_engine.Identifiers.PageId> UnreclaimedPageIds { get; }
}
