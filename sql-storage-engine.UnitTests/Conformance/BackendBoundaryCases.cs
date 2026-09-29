using SqlExecutionEngine.Storage.Abstractions;

namespace SqlExecutionEngine.Storage.Conformance;

internal static class BackendBoundaryCases
{
    internal static async Task RunAsync(BackendRegistration provider, CancellationToken token)
    {
        var key = Enumerable.Repeat((byte)255, 256).ToArray();
        var value = Enumerable.Repeat((byte)91, 1048576).ToArray();
        await RoundTripAsync(provider, [new(BackendOracle.Collection, new(key), new(value))], token);
        await BackendOracle.ThrowsAsync<ArgumentException>(() =>
        {
            _ = new BackendMutation(BackendOracle.Collection, new(new byte[257]), new([])); return Task.CompletedTask;
        });
        await BackendOracle.ThrowsAsync<ArgumentException>(() =>
        {
            _ = new BackendMutation(BackendOracle.Collection, new([1]), new(new byte[1048577])); return Task.CompletedTask;
        });
        var mutations = Enumerable.Range(0, 1024).Select(i => new BackendMutation(BackendOracle.Collection,
            new([(byte)(i >> 8), (byte)i]), new([42]))).ToArray();
        await RoundTripAsync(provider, mutations, token);
        await BackendOracle.ThrowsAsync<ArgumentException>(() =>
        {
            _ = MakeBatch(mutations.Append(new(BackendOracle.Collection, new([255, 255]), new([])))); return Task.CompletedTask;
        });
        var exactBytes = Enumerable.Range(0, 4).Select(i => new BackendMutation(BackendOracle.Collection,
            new([(byte)i]), new(Enumerable.Repeat((byte)(i + 1), i == 3 ? 1048420 : 1048576).ToArray()))).ToArray();
        await RoundTripAsync(provider, exactBytes, token); // 52 + 4 * 26 + payload = 4 MiB.
        exactBytes[3] = new(BackendOracle.Collection, new([3]), new(new byte[1048421]));
        await BackendOracle.ThrowsAsync<ArgumentException>(() => { _ = MakeBatch(exactBytes); return Task.CompletedTask; });
    }

    private static BackendBatch MakeBatch(IEnumerable<BackendMutation> mutations) =>
        new(new(Guid.NewGuid()), new(Guid.NewGuid()), new(new(new byte[16])), mutations);

    private static async Task RoundTripAsync(BackendRegistration provider, BackendMutation[] mutations, CancellationToken token)
    {
        await using var location = await provider.CreateLocation(token);
        var factory = provider.Driver!.CreateFactory(new());
        BackendBatch batch;
        CommitReceipt receipt;
        await using (var store = await BackendAdmission.OpenAsync(factory, new(location.Location, BackendOpenMode.CreateNew), token))
        {
            await using var before = await store.OpenSnapshotAsync(token);
            batch = BackendOracle.Batch(store, before, mutations);
            receipt = await store.CommitAsync(batch, token);
            BackendOracle.Receipt(receipt, batch, CommitStatus.Committed);
            await BackendOracle.RowsAsync(before, new(), [], token);
        }
        await using var reopened = await BackendAdmission.OpenAsync(factory, new(location.Location, BackendOpenMode.OpenExisting), token);
        BackendOracle.Require(await reopened.ResolveAsync(batch.Operation, token) == receipt, "Boundary receipt was lost.");
        await using var read = await reopened.OpenSnapshotAsync(token);
        await BackendOracle.RowsAsync(read, new(), mutations.Select(m => (m.Key.ToArray(), m.Value!.ToArray())).ToArray(), token);
    }
}
