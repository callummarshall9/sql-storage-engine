using SqlExecutionEngine.Storage.Abstractions;

namespace SqlExecutionEngine.Storage.Conformance;

internal static class BackendQuotaCases
{
    internal static async Task RunAsync(BackendRegistration provider, CancellationToken token)
    {
        await SmallQuotasAsync(provider, token);
        await BackendBoundaryCases.RunAsync(provider, token);
        await BackendIdentityCases.RunAsync(provider, token);
    }

    private static async Task SmallQuotasAsync(BackendRegistration provider, CancellationToken token)
    {
        await using var location = await provider.CreateLocation(token);
        var factory = provider.Driver!.CreateFactory(new(StoreBytes: 27, Receipts: 2, Snapshots: 2));
        await using var store = await BackendAdmission.OpenAsync(factory, new(location.Location, BackendOpenMode.CreateNew), token);
        var limits = store.Descriptor.Limits;
        BackendOracle.Require(limits.StoreBytes == 27 && limits.Receipts == 2 && limits.Snapshots == 2,
            "Host did not configure requested limits.");
        await using var before = await store.OpenSnapshotAsync(token);
        var first = BackendOracle.Batch(store, before, new BackendMutation(BackendOracle.Collection, new([1]), new([42])));
        var receipt = await store.CommitAsync(first, token);
        BackendOracle.Receipt(receipt, first, CommitStatus.Committed);
        await using var after = await store.OpenSnapshotAsync(token); // Exactly two snapshots.
        await BackendOracle.ThrowsAsync<BackendResourceException>(async () => await store.OpenSnapshotAsync(token));
        // Same key with one more byte: exactly one byte above the live-record charge of 27.
        var over = BackendOracle.Batch(store, after, new BackendMutation(BackendOracle.Collection, new([1]), new([42, 43])));
        await BackendOracle.ThrowsAsync<BackendResourceException>(async () => await store.CommitAsync(over, token));
        BackendOracle.Receipt(await store.ResolveAsync(over.Operation, token), over, CommitStatus.NotSubmitted);
        var stale = BackendOracle.Batch(store, before, new BackendMutation(BackendOracle.Collection, new([1]), new([43])));
        BackendOracle.Receipt(await store.CommitAsync(stale, token), stale, CommitStatus.Conflict); // Exactly two receipts.
        var third = BackendOracle.Batch(store, after, new BackendMutation(BackendOracle.Collection, new([1]), new([43])));
        await BackendOracle.ThrowsAsync<BackendResourceException>(async () => await store.CommitAsync(third, token));
        BackendOracle.Receipt(await store.ResolveAsync(third.Operation, token), third, CommitStatus.NotSubmitted);
        BackendOracle.Require(await store.CommitAsync(first, token) == receipt, "Full receipt quota rejected replay.");
        await BackendOracle.RowsAsync(after, new(), [([1], [42])], token);
        await after.DisposeAsync();
        await using var replacement = await store.OpenSnapshotAsync(token);
        await BackendOracle.RowsAsync(replacement, new(), [([1], [42])], token);
    }
}
