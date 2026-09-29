using System.Diagnostics;
using System.Text;
using SqlExecutionEngine.Storage.Abstractions;
using SqlExecutionEngine.Storage.Conformance;
using SqlStorageEngine.Core;

namespace sql_storage_engine.UnitTests;

internal sealed class PageConformanceDriver : BackendTestDriver
{
    internal static BackendRegistration Registration() => new("page-core-1.0.0", new PageConformanceDriver().CreateFactory(new()), _ =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "page-conformance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return ValueTask.FromResult(new BackendTestLocation(new(Encoding.UTF8.GetBytes(Path.Combine(directory, "store"))),
            () => { Directory.Delete(directory, true); return ValueTask.CompletedTask; }));
    }, new PageConformanceDriver());

    public override IBackendFactory CreateFactory(BackendTestSettings settings, Func<BackendTestCheckpoint, ValueTask>? checkpoint = null) =>
        new PageBackendFactory(PathFor, new(settings.StoreBytes, settings.Receipts, settings.Snapshots, settings.SnapshotLifetime))
        {
            Checkpoint = checkpoint is null ? null : stage => checkpoint(stage switch
            {
                "admission-written" => BackendTestCheckpoint.AdmissionWritten,
                "admission-committed" => BackendTestCheckpoint.AdmissionDurable,
                "publication-written" => BackendTestCheckpoint.PublicationWritten,
                "publication-committed" => BackendTestCheckpoint.PublicationDurable,
                _ => throw new InvalidOperationException("Unexpected persistence checkpoint.")
            })
        };

    public override ProcessStartInfo CreateCrashProcess(ByteString location, BackendTestCheckpoint checkpoint,
        OperationId operation, CollectionId collection)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "sql-storage-engine.sln"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Probe build root was not found.");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var start = new ProcessStartInfo("dotnet");
        start.ArgumentList.Add(Path.Combine(root.FullName, "tests/TransactionCrashProbe/bin", configuration, "net10.0/TransactionCrashProbe.dll"));
        start.ArgumentList.Add(PathFor(location));
        start.ArgumentList.Add("raw");
        start.ArgumentList.Add(checkpoint switch
        {
            BackendTestCheckpoint.AdmissionWritten => "admission-written",
            BackendTestCheckpoint.AdmissionDurable => "admission-committed",
            BackendTestCheckpoint.PublicationWritten => "publication-written",
            BackendTestCheckpoint.PublicationDurable => "publication-committed",
            _ => throw new ArgumentOutOfRangeException(nameof(checkpoint))
        });
        start.ArgumentList.Add(operation.Value.ToString());
        start.ArgumentList.Add(collection.Value.ToString());
        return start;
    }

    public override ValueTask<IAsyncDisposable> InjectIoFailureAsync(ByteString location, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // Remove the flushed staging file: the actual publication rename must fail.
        File.Delete(PathFor(location) + ".pending");
        return ValueTask.FromResult<IAsyncDisposable>(new PagePublicationFault());
    }

    private static string PathFor(ByteString location) => Encoding.UTF8.GetString(location.ToArray());
}
