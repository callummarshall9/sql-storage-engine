using SqlExecutionEngine.Storage.Abstractions;

namespace SqlStorageEngine.Core;

/// <summary>Finite limits for the initial whole-image page profile. No optional accelerator is advertised.</summary>
public sealed class PageBackendOptions
{
    public PageBackendOptions(long storeBytes = 8 * 1024 * 1024, int receipts = 1024,
        int snapshots = 8, TimeSpan? snapshotLifetime = null)
    {
        if (storeBytes is < 1 or > 8 * 1024 * 1024 || receipts is < 1 or > 1024 || snapshots is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(storeBytes));
        var lifetime = snapshotLifetime ?? TimeSpan.FromMinutes(5);
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromHours(1))
            throw new ArgumentOutOfRangeException(nameof(snapshotLifetime));
        Limits = new(256, 1048576, 1024, 4194304, storeBytes, receipts, snapshots, 1, lifetime);
    }

    public BackendLimits Limits { get; }
    internal BackendDescriptor Descriptor => new(1, 0, "DurableAtomicV1",
        BackendGuarantees.ConsistentSnapshots | BackendGuarantees.ConditionalAtomicBatches | BackendGuarantees.DurableReceipts,
        Limits);
}
