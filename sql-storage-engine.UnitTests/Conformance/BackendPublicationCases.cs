using SqlExecutionEngine.Storage.Abstractions;

namespace SqlExecutionEngine.Storage.Conformance;

internal static class BackendPublicationCases
{
    internal static async Task RunAsync(BackendRegistration provider, CancellationToken token)
    {
        await PublishAsync(provider, false, token);
        await PublishAsync(provider, true, token);
    }

    private static async Task PublishAsync(BackendRegistration provider, bool disposeDuringPublication, CancellationToken token)
    {
        await using var location = await provider.CreateLocation(token);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = provider.Driver!.CreateFactory(new(), async phase =>
        {
            if (phase == BackendTestCheckpoint.PublicationWritten) { reached.TrySetResult(); await release.Task; }
        });
        BackendBatch batch;
        await using (var store = await BackendAdmission.OpenAsync(factory, new(location.Location, BackendOpenMode.CreateNew), token))
        {
            BackendOracle.Require(store.Descriptor.Limits.ConcurrentOperations == 1, "Driver must configure one concurrent operation.");
            await using var before = await store.OpenSnapshotAsync(token);
            batch = BackendOracle.Batch(store, before, new BackendMutation(BackendOracle.Collection, new([1]), new([42])));
            var competitor = BackendOracle.Batch(store, before, new BackendMutation(BackendOracle.Collection, new([2]), new([43])));
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            var commit = store.CommitAsync(batch, cancellation.Token).AsTask();
            Task? disposal = null;
            try
            {
                await reached.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
                await BackendOracle.ThrowsAsync<BackendResourceException>(async () => await store.OpenSnapshotAsync(token));
                await BackendOracle.ThrowsAsync<BackendResourceException>(async () => await store.CommitAsync(competitor, token));
                cancellation.Cancel(); // Admission is durable; cancellation cannot claim rollback.
                if (disposeDuringPublication)
                {
                    disposal = store.DisposeAsync().AsTask();
                    BackendOracle.Require(!disposal.IsCompleted, "Disposal abandoned active ownership.");
                }
            }
            finally
            {
                release.TrySetResult();
                try { BackendOracle.Receipt(await commit, batch, CommitStatus.Committed); }
                finally { if (disposal is not null) await disposal; }
            }
            if (!disposeDuringPublication)
            {
                BackendOracle.Receipt(await store.ResolveAsync(competitor.Operation, token), competitor, CommitStatus.NotSubmitted);
                BackendOracle.Receipt(await store.CommitAsync(competitor, token), competitor, CommitStatus.Conflict);
                await BackendOracle.RowsAsync(before, new(), [], token);
            }
        }
        await using var recovered = await BackendAdmission.OpenAsync(provider.Factory, new(location.Location, BackendOpenMode.OpenExisting), token);
        BackendOracle.Receipt(await recovered.ResolveAsync(batch.Operation, token), batch, CommitStatus.Committed);
        await using var read = await recovered.OpenSnapshotAsync(token);
        await BackendOracle.RowsAsync(read, new(), [([1], [42])], token);
    }
}
