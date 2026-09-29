using SqlExecutionEngine.Storage.Abstractions;

namespace SqlExecutionEngine.Storage.Conformance;

internal static class BackendContractCases
{
    internal static async Task AtomicSnapshotAsync(IBackendFactory factory, ByteString location, CancellationToken token)
    {
        await using var store = await BackendAdmission.OpenAsync(factory, new(location, BackendOpenMode.CreateNew), token);
        await using var before = await store.OpenSnapshotAsync(token);
        BackendOracle.Require(before.Store == store.Id, "Snapshot belongs to another store.");
        var batch = BackendOracle.Batch(store, before,
            new BackendMutation(BackendOracle.Collection, new([128]), new([2])),
            new BackendMutation(BackendOracle.Collection, new([1]), new([])),
            new BackendMutation(BackendOracle.Collection, new([127]), new([3])));
        var receipt = await store.CommitAsync(batch, token);
        BackendOracle.Receipt(receipt, batch, CommitStatus.Committed);
        BackendOracle.Require(receipt.Generation != before.Generation, "Commit did not advance generation.");
        BackendOracle.Require(await before.ReadAsync(BackendOracle.Collection, new([1]), token) is null, "Old snapshot changed.");
        await BackendOracle.RowsAsync(before, new(), [], token);
        await using var after = await store.OpenSnapshotAsync(token);
        BackendOracle.Require(after.Generation == receipt.Generation, "Snapshot does not observe committed generation.");
        BackendOracle.Require((await after.ReadAsync(BackendOracle.Collection, new([1]), token))?.Length == 0, "Present-empty became missing.");
        BackendOracle.Require(await after.ReadAsync(BackendOracle.Collection, new([2]), token) is null, "Missing became present.");
        await BackendOracle.RowsAsync(after, new(), [([1], []), ([127], [3]), ([128], [2])], token);
        await BackendOracle.RowsAsync(after, new(new([127]), new([128])), [([127], [3])], token);
        await BackendOracle.RowsAsync(after, new(new([127]), new([127])), [], token);
        var stale = BackendOracle.Batch(store, before, new BackendMutation(BackendOracle.Collection, new([9]), new([9])));
        BackendOracle.Receipt(await store.CommitAsync(stale, token), stale, CommitStatus.Conflict);
        BackendOracle.Require(await store.CommitAsync(batch, token) == receipt, "Identical retry changed outcome.");
        BackendOracle.Require(await store.ResolveAsync(batch.Operation, token) == receipt, "Resolve disagrees with commit.");
        await BackendOracle.ThrowsAsync<ArgumentException>(async () => await store.CommitAsync(
            new(batch.Operation, store.Id, before.Generation, [new BackendMutation(BackendOracle.Collection, new([1]), new([99]))]), token));
        await using var final = await store.OpenSnapshotAsync(token);
        await BackendOracle.RowsAsync(final, new(), [([1], []), ([127], [3]), ([128], [2])], token);
    }

    internal static async Task ReopenAsync(IBackendFactory factory, ByteString location, CancellationToken token)
    {
        StoreId id;
        BackendBatch batch;
        CommitReceipt receipt;
        CommitReceipt conflict;
        await using (var store = await BackendAdmission.OpenAsync(factory, new(location, BackendOpenMode.CreateNew), token))
        {
            id = store.Id;
            await using var snapshot = await store.OpenSnapshotAsync(token);
            batch = BackendOracle.Batch(store, snapshot, new BackendMutation(BackendOracle.Collection, new([1]), new([7])));
            receipt = await store.CommitAsync(batch, token);
            BackendOracle.Receipt(receipt, batch, CommitStatus.Committed);
            var stale = BackendOracle.Batch(store, snapshot, new BackendMutation(BackendOracle.Collection, new([2]), new([8])));
            conflict = await store.CommitAsync(stale, token);
            BackendOracle.Receipt(conflict, stale, CommitStatus.Conflict);
        }
        await using var reopened = await BackendAdmission.OpenAsync(factory, new(location, BackendOpenMode.OpenExisting), token);
        BackendOracle.Require(reopened.Id == id, "Reopen changed store identity.");
        await using var read = await reopened.OpenSnapshotAsync(token);
        await BackendOracle.RowsAsync(read, new(), [([1], [7])], token);
        BackendOracle.Require(await reopened.ResolveAsync(batch.Operation, token) == receipt, "Reopen lost committed receipt.");
        BackendOracle.Require(await reopened.ResolveAsync(conflict.Operation, token) == conflict, "Reopen lost conflict receipt.");
        BackendOracle.Require(await reopened.CommitAsync(batch, token) == receipt, "Reopen retry changed receipt.");
    }

    internal static async Task LifetimeAsync(IBackendFactory factory, ByteString location, CancellationToken token)
    {
        await using var store = await BackendAdmission.OpenAsync(factory, new(location, BackendOpenMode.CreateNew), token);
        await using var snapshot = await store.OpenSnapshotAsync(token);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var batch = BackendOracle.Batch(store, snapshot, new BackendMutation(BackendOracle.Collection, new([1]), new([2])));
        await BackendOracle.ThrowsAsync<OperationCanceledException>(async () => await store.CommitAsync(batch, canceled.Token));
        var outcome = await store.ResolveAsync(batch.Operation, token);
        BackendOracle.Receipt(outcome, batch, CommitStatus.NotSubmitted);
        await BackendOracle.RowsAsync(snapshot, new(), [], token);
        await BackendOracle.ThrowsAsync<ArgumentException>(async () => await snapshot.ReadAsync(default, new([1]), token));
        await BackendOracle.ThrowsAsync<ArgumentException>(async () => await store.ResolveAsync(default, token));
        await snapshot.DisposeAsync();
        await BackendOracle.ThrowsAsync<ObjectDisposedException>(async () => await snapshot.ReadAsync(BackendOracle.Collection, new([1]), token));
        await using var owned = await store.OpenSnapshotAsync(token);
        await store.DisposeAsync();
        await BackendOracle.ThrowsAsync<ObjectDisposedException>(async () => await store.OpenSnapshotAsync(token));
        await BackendOracle.ThrowsAsync<ObjectDisposedException>(async () => await owned.ReadAsync(BackendOracle.Collection, new([1]), token));
    }
}
