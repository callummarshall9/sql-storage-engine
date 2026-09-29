using SqlExecutionEngine.Storage.Abstractions;

namespace SqlExecutionEngine.Storage.Conformance;

internal static class BackendIdentityCases
{
    internal static async Task RunAsync(BackendRegistration provider, CancellationToken token)
    {
        await using var location = await provider.CreateLocation(token);
        var factory = provider.Driver!.CreateFactory(new());
        var other = new CollectionId(new Guid("f175d411-c688-4d9e-899e-341ef8caa002"));
        BackendBatch unsubmitted;
        await using (var store = await BackendAdmission.OpenAsync(factory, new(location.Location, BackendOpenMode.CreateNew), token))
        {
            await using var before = await store.OpenSnapshotAsync(token);
            var foreign = new BackendBatch(new(Guid.NewGuid()), new(Guid.NewGuid()), before.Generation,
                [new BackendMutation(BackendOracle.Collection, new([1]), new([9]))]);
            await BackendOracle.ThrowsAsync<ArgumentException>(async () => await store.CommitAsync(foreign, token));
            BackendOracle.Require((await store.ResolveAsync(foreign.Operation, token)).Status == CommitStatus.NotSubmitted,
                "Foreign batch was admitted.");
            var put = BackendOracle.Batch(store, before, new BackendMutation(BackendOracle.Collection, new([1]), new([])), new(other, new([1]), new([9])));
            BackendOracle.Receipt(await store.CommitAsync(put, token), put, CommitStatus.Committed);
            await using var after = await store.OpenSnapshotAsync(token);
            var delete = BackendOracle.Batch(store, after, new BackendMutation(BackendOracle.Collection, new([1]), null));
            BackendOracle.Receipt(await store.CommitAsync(delete, token), delete, CommitStatus.Committed);
            await using var deleted = await store.OpenSnapshotAsync(token);
            await BackendOracle.RowsAsync(deleted, new(), [], token);
            BackendOracle.Require((await deleted.ReadAsync(other, new([1]), token))!.ToArray().SequenceEqual(new byte[] { 9 }), "Collections collided.");
            BackendOracle.Require((await after.ReadAsync(BackendOracle.Collection, new([1]), token))?.Length == 0, "Deletion changed old snapshot.");
            var noOp = BackendOracle.Batch(store, deleted, new BackendMutation(BackendOracle.Collection, new([2]), null));
            var receipt = await store.CommitAsync(noOp, token);
            BackendOracle.Receipt(receipt, noOp, CommitStatus.Committed);
            BackendOracle.Require(receipt.Generation != deleted.Generation, "No-op did not advance generation.");
            await using var final = await store.OpenSnapshotAsync(token);
            unsubmitted = BackendOracle.Batch(store, final, new BackendMutation(BackendOracle.Collection, new([2]), new([9])));
        }
        await using var reopened = await BackendAdmission.OpenAsync(factory, new(location.Location, BackendOpenMode.OpenExisting), token);
        BackendOracle.Receipt(await reopened.CommitAsync(unsubmitted, token), unsubmitted, CommitStatus.Conflict);
        await using var read = await reopened.OpenSnapshotAsync(token);
        await BackendOracle.RowsAsync(read, new(), [], token);
    }
}
