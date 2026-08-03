using sql_storage_engine.Heap;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Diagnostics;

public sealed record HeapMaintenanceProgress(int NextPageIndex, long ReclaimedBytes, long FailureCount, bool Complete);
public interface IHeapMaintenanceCheckpoint
{
    ValueTask<HeapMaintenanceProgress?> ReadAsync(CancellationToken cancellationToken = default);
    ValueTask WriteAsync(HeapMaintenanceProgress progress, CancellationToken cancellationToken = default);
}
public sealed class MemoryHeapMaintenanceCheckpoint : IHeapMaintenanceCheckpoint
{
    public HeapMaintenanceProgress? Progress { get; private set; }
    public ValueTask<HeapMaintenanceProgress?> ReadAsync(CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(Progress); }
    public ValueTask WriteAsync(HeapMaintenanceProgress progress, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); Progress = progress; return ValueTask.CompletedTask; }
}

/// <summary>Compacts a bounded heap batch, checkpoints every page boundary, and yields between pages.</summary>
public sealed class HeapMaintenance(TableHeap heap, IHeapMaintenanceCheckpoint checkpoint,
    LockManager lockManager, TableId tableId, Func<CancellationToken, ValueTask>? throttle = null)
{
    public async Task<HeapMaintenanceProgress> RunBatchAsync(int maximumPages,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPages);
        var prior = await checkpoint.ReadAsync(cancellationToken).ConfigureAwait(false) ?? new(0, 0, 0, false);
        var pages = await heap.GetPageIdsAsync(StorageLimits.MaximumScanPages, cancellationToken).ConfigureAwait(false);
        var transactionId = new TransactionId(ulong.MaxValue - 1);
        await lockManager.AcquireAsync(transactionId, new TableLockResource(tableId), LockMode.Update, cancellationToken)
            .ConfigureAwait(false);
        var progress = prior;
        try
        {
            var end = Math.Min(pages.Count, checked(prior.NextPageIndex + maximumPages));
            for (var index = prior.NextPageIndex; index < end; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var result = await heap.CompactPageWithResultAsync(pages[index], cancellationToken).ConfigureAwait(false);
                    progress = progress with { NextPageIndex = index + 1,
                        ReclaimedBytes = checked(progress.ReclaimedBytes + result.ReclaimedBytes), Complete = index + 1 == pages.Count };
                }
                catch when (!cancellationToken.IsCancellationRequested)
                { progress = progress with { NextPageIndex = index + 1, FailureCount = progress.FailureCount + 1 }; }
                await checkpoint.WriteAsync(progress, CancellationToken.None).ConfigureAwait(false);
                if (throttle is not null) await throttle(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            return progress;
        }
        finally { lockManager.Release(transactionId, new TableLockResource(tableId)); }
    }
}
