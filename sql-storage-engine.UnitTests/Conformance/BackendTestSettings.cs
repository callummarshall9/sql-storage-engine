namespace SqlExecutionEngine.Storage.Conformance;

/// <summary>Small, explicit host configurations used by the independent quota oracles.</summary>
public sealed record BackendTestSettings(long StoreBytes = 8388608, int Receipts = 1024,
    int Snapshots = 8, TimeSpan? SnapshotLifetime = null);
