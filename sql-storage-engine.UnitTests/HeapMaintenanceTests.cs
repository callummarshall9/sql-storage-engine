using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Heap;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;

public sealed class HeapMaintenanceTests
{
    [Test]
    public async Task CancellationLeavesRowsValidAndRestartContinuesFromCheckpoint()
    {
        await using var store = new InMemoryPageStore();
        await using var pool = new BufferPool(store, 8, leaveOpen: true);
        var heap = await TableHeap.CreateAsync(pool, store);
        var row = await heap.InsertAsync(new byte[] { 1, 2, 3 });
        var checkpoint = new MemoryHeapMaintenanceCheckpoint();
        using var cancellation = new CancellationTokenSource();
        var maintenance = new HeapMaintenance(heap, checkpoint, new LockManager(), new TableId(1), _ =>
        { cancellation.Cancel(); return ValueTask.CompletedTask; });

        await ((Func<Task>)(async () => await maintenance.RunBatchAsync(2, cancellation.Token)))
            .Should().ThrowAsync<OperationCanceledException>();
        (await heap.ReadAsync(row)).Result.Should().Be(TableHeapLookupResult.Found);
        checkpoint.Progress!.NextPageIndex.Should().Be(1);

        var resumed = await new HeapMaintenance(heap, checkpoint, new LockManager(), new TableId(1)).RunBatchAsync(2);
        resumed.Complete.Should().BeTrue();
        (await heap.ReadAsync(row)).Result.Should().Be(TableHeapLookupResult.Found);
    }

    [Test]
    public async Task BatchIsBoundedAndForegroundSharedTableLockCanProceed()
    {
        await using var store = new InMemoryPageStore(4096);
        await using var pool = new BufferPool(store, 8, leaveOpen: true);
        var heap = await TableHeap.CreateAsync(pool, store);
        await heap.InsertAsync(new byte[3000]); await heap.InsertAsync(new byte[3000]);
        var checkpoint = new MemoryHeapMaintenanceCheckpoint(); var locks = new LockManager();
        var maintenance = new HeapMaintenance(heap, checkpoint, locks, new TableId(1));
        var first = await maintenance.RunBatchAsync(1);
        first.NextPageIndex.Should().Be(1); first.Complete.Should().BeFalse();
        await locks.AcquireAsync(new TransactionId(1), new TableLockResource(new TableId(1)), LockMode.Shared);
        locks.ReleaseAll(new TransactionId(1));
        var final = await maintenance.RunBatchAsync(10);
        final.Complete.Should().BeTrue(); final.FailureCount.Should().Be(0);
    }
}
