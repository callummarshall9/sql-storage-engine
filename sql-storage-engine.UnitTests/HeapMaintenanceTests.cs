using sql_storage_engine.Exposures.Diagnostics;
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Heap;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Transactions;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Exposures.LockResources;

namespace sql_storage_engine.UnitTests;
public sealed class HeapMaintenanceTests
{
    [Test]
    public async Task CancellationLeavesRowsValidAndRestartContinuesFromCheckpoint()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 8, leaveOpen: true);
        var heap = await sql_storage_engine.Exposures.Heap.TableHeapExposure.CreateTableHeapExposureAsync(pool,store);
        var row = await heap.InsertAsync(new byte[] { 1, 2, 3 });
        var checkpoint = new MemoryHeapMaintenanceCheckpointExposure();
        using var cancellation = new CancellationTokenSource();
        var maintenance = new HeapMaintenanceExposure(heap, checkpoint, new sql_storage_engine.Exposures.Transactions.LockManagerExposure(), new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, _ =>
        {
            cancellation.Cancel();
            return ValueTask.CompletedTask;
        });
        await ((Func<Task>)(async () => await maintenance.RunBatchAsync(2, cancellation.Token))).Should().ThrowAsync<OperationCanceledException>();
        (await heap.ReadAsync(row)).Result.Should().Be(TableHeapLookupResult.Found);
        (await checkpoint.ReadAsync())!.NextPageIndex.Should().Be(1);
        var resumed = await new HeapMaintenanceExposure(heap, checkpoint, new sql_storage_engine.Exposures.Transactions.LockManagerExposure(), new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }).RunBatchAsync(2);
        resumed.Complete.Should().BeTrue();
        (await heap.ReadAsync(row)).Result.Should().Be(TableHeapLookupResult.Found);
    }

    [Test]
    public async Task BatchIsBoundedAndForegroundSharedTableLockCanProceed()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure(4096);
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 8, leaveOpen: true);
        var heap = await sql_storage_engine.Exposures.Heap.TableHeapExposure.CreateTableHeapExposureAsync(pool,store);
        await heap.InsertAsync(new byte[3000]);
        await heap.InsertAsync(new byte[3000]);
        var checkpoint = new MemoryHeapMaintenanceCheckpointExposure();
        var locks = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        var maintenance = new HeapMaintenanceExposure(heap, checkpoint, locks, new sql_storage_engine.Models.Identifiers.TableId { Value = 1 });
        var first = await maintenance.RunBatchAsync(1);
        first.NextPageIndex.Should().Be(1);
        first.Complete.Should().BeFalse();
        await locks.AcquireAsync(new sql_storage_engine.Models.Identifiers.TransactionId { Value = 1 },sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateTableLockResource(new sql_storage_engine.Models.LockResources.TableLockResource { TableId = new sql_storage_engine.Models.Identifiers.TableId { Value = 1 } }),LockMode.Shared);
        locks.ReleaseAll(new sql_storage_engine.Models.Identifiers.TransactionId { Value = 1 });
        var final = await maintenance.RunBatchAsync(10);
        final.Complete.Should().BeTrue();
        final.FailureCount.Should().Be(0);
    }
    [Test]
    public async Task DisposingMaintenanceClosesItsAdapterAndKeepsBorrowedResourcesAvailable()
    {
        await using var store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using var pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 8, leaveOpen: true);
        var heap = await sql_storage_engine.Exposures.Heap.TableHeapExposure.CreateTableHeapExposureAsync(pool, store);
        var rowId = await heap.InsertAsync(new byte[] { 1, 2, 3 });
        var checkpoint = new MemoryHeapMaintenanceCheckpointExposure();
        var maintenance = new HeapMaintenanceExposure(heap, checkpoint, new sql_storage_engine.Exposures.Transactions.LockManagerExposure(), new TableId { Value = 1 });
        var progress = await maintenance.RunBatchAsync(1);
        maintenance.Dispose();
        await ((Func<Task>)(async () => await maintenance.RunBatchAsync(1))).Should().ThrowAsync<ObjectDisposedException>();
        (await heap.ReadAsync(rowId)).Result.Should().Be(TableHeapLookupResult.Found);
        (await checkpoint.ReadAsync()).Should().Be(progress);
    }
}