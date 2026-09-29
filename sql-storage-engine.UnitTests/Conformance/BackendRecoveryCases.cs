using System.Diagnostics;
using SqlExecutionEngine.Storage.Abstractions;

namespace SqlExecutionEngine.Storage.Conformance;

internal static class BackendRecoveryCases
{
    internal static async Task ProcessExitAsync(BackendRegistration provider, CancellationToken token)
    {
        foreach (var (checkpoint, expected) in new[] {
            (BackendTestCheckpoint.AdmissionWritten, CommitStatus.NotSubmitted),
            (BackendTestCheckpoint.AdmissionDurable, CommitStatus.Conflict),
            (BackendTestCheckpoint.PublicationWritten, CommitStatus.Conflict),
            (BackendTestCheckpoint.PublicationDurable, CommitStatus.Committed) })
        {
            await using var location = await provider.CreateLocation(token);
            var operation = new OperationId(Guid.NewGuid());
            var start = provider.Driver!.CreateCrashProcess(location.Location, checkpoint, operation, BackendOracle.Collection);
            start.UseShellExecute = false; start.RedirectStandardOutput = true; start.RedirectStandardError = true;
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Child did not start.");
            var errors = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
            try
            {
                // Bounded protocol: never accumulate arbitrary provider output or include it in the report.
                var marker = new char[11];
                await process.StandardOutput.ReadBlockAsync(marker.AsMemory(), token).AsTask().WaitAsync(TimeSpan.FromSeconds(30), token);
                BackendOracle.Require(new string(marker) == "CHECKPOINT\n", "Child did not reach the requested boundary.");
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(10), token);
            }
            finally
            {
                if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
                await errors.WaitAsync(TimeSpan.FromSeconds(10));
            }
            await using var recovered = await BackendAdmission.OpenAsync(provider.Factory, new(location.Location, BackendOpenMode.OpenExisting), token);
            var receipt = await recovered.ResolveAsync(operation, token);
            BackendOracle.Require(receipt.Store == recovered.Id && receipt.Operation == operation && receipt.Status == expected,
                "Recovery outcome differs from durable boundary.");
            await using var snapshot = await recovered.OpenSnapshotAsync(token);
            await BackendOracle.RowsAsync(snapshot, new(), expected == CommitStatus.Committed ? [([1], [42])] : [], token);
            BackendOracle.Require(await recovered.ResolveAsync(operation, token) == receipt, "Recovery receipt is unstable.");
        }
    }

    internal static async Task IoFailureAsync(BackendRegistration provider, CancellationToken token)
    {
        await using var location = await provider.CreateLocation(token);
        IAsyncDisposable? fault = null;
        var factory = provider.Driver!.CreateFactory(new(), async phase =>
        {
            if (phase == BackendTestCheckpoint.PublicationWritten)
                fault = await provider.Driver.InjectIoFailureAsync(location.Location, token);
        });
        BackendBatch batch;
        try
        {
            await using var store = await BackendAdmission.OpenAsync(factory, new(location.Location, BackendOpenMode.CreateNew), token);
            await using var before = await store.OpenSnapshotAsync(token);
            batch = BackendOracle.Batch(store, before, new BackendMutation(BackendOracle.Collection, new([1]), new([42])));
            BackendOracle.Receipt(await store.CommitAsync(batch, token), batch, CommitStatus.Unknown);
            BackendOracle.Require(fault is not null, "No I/O fault was injected.");
            BackendOracle.Receipt(await store.ResolveAsync(batch.Operation, token), batch, CommitStatus.Unknown);
            await BackendOracle.ThrowsAsync<InvalidOperationException>(async () => await store.OpenSnapshotAsync(token));
            await BackendOracle.RowsAsync(before, new(), [], token);
        }
        finally { if (fault is not null) await fault.DisposeAsync(); }
        await using var recovered = await BackendAdmission.OpenAsync(provider.Factory, new(location.Location, BackendOpenMode.OpenExisting), token);
        BackendOracle.Receipt(await recovered.ResolveAsync(batch.Operation, token), batch, CommitStatus.Conflict);
        await using var read = await recovered.OpenSnapshotAsync(token);
        await BackendOracle.RowsAsync(read, new(), [], token);
    }
}
