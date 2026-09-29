using SqlExecutionEngine.Storage.Abstractions;

namespace SqlExecutionEngine.Storage.Conformance;

internal static class BackendExpiryCases
{
    internal static async Task RunAsync(BackendRegistration provider, CancellationToken token)
    {
        await using var location = await provider.CreateLocation(token);
        var lifetime = TimeSpan.FromMilliseconds(50);
        var factory = provider.Driver!.CreateFactory(new(Snapshots: 1, SnapshotLifetime: lifetime));
        await using var store = await BackendAdmission.OpenAsync(factory, new(location.Location, BackendOpenMode.CreateNew), token);
        BackendOracle.Require(store.Descriptor.Limits.SnapshotLifetime == lifetime && store.Descriptor.Limits.Snapshots == 1,
            "Driver did not configure expiry profile.");
        await using var expired = await store.OpenSnapshotAsync(token);
        await Task.Delay(lifetime + TimeSpan.FromMilliseconds(25), token);
        await BackendOracle.ThrowsAsync<BackendResourceException>(async () => await expired.ReadAsync(BackendOracle.Collection, new([1]), token));
        await BackendOracle.ThrowsAsync<BackendResourceException>(async () => await BackendOracle.RowsAsync(expired, new(), [], token));
        await expired.DisposeAsync();
        await using var replacement = await store.OpenSnapshotAsync(token);
        await BackendOracle.RowsAsync(replacement, new(), [], token);

        // Enumeration paused between rows must not retain an operation slot or strand store disposal.
        await using var scanLocation = await provider.CreateLocation(token);
        await using var scanStore = await BackendAdmission.OpenAsync(provider.Factory, new(scanLocation.Location, BackendOpenMode.CreateNew), token);
        await using var initial = await scanStore.OpenSnapshotAsync(token);
        var batch = BackendOracle.Batch(scanStore, initial, new BackendMutation(BackendOracle.Collection, new([1]), new([42])),
            new BackendMutation(BackendOracle.Collection, new([2]), new([43])));
        BackendOracle.Receipt(await scanStore.CommitAsync(batch, token), batch, CommitStatus.Committed);
        await using var snapshot = await scanStore.OpenSnapshotAsync(token);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(token);
        await using var rows = snapshot.ScanAsync(BackendOracle.Collection, new(), canceled.Token).GetAsyncEnumerator(canceled.Token);
        BackendOracle.Require(await rows.MoveNextAsync(), "Missing initial scan row.");
        canceled.Cancel();
        await BackendOracle.ThrowsAsync<OperationCanceledException>(async () => await rows.MoveNextAsync());
        await rows.DisposeAsync();
        await scanStore.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10), token);
        await BackendOracle.ThrowsAsync<ObjectDisposedException>(async () => await snapshot.ReadAsync(BackendOracle.Collection, new([1]), token));
    }
}
