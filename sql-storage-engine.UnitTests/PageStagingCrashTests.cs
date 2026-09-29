using System.Diagnostics;
using System.Text;
using SqlExecutionEngine.Storage.Abstractions;
using SqlExecutionEngine.Storage.Conformance;
using SqlStorageEngine.Core;

namespace sql_storage_engine.UnitTests;

public sealed class PageStagingCrashTests
{
    [TestCase(BackendTestCheckpoint.AdmissionWritten, CommitStatus.NotSubmitted)]
    [TestCase(BackendTestCheckpoint.PublicationWritten, CommitStatus.Conflict)]
    public async Task KilledPublisherCannotAuthorizeDeletionOfNeighborFilesAndRecoveryCanWriteAgain(
        BackendTestCheckpoint checkpoint, CommitStatus expected)
    {
        var directory = Directory.CreateTempSubdirectory("page-staging-crash-").FullName;
        try
        {
            var path = Path.Combine(directory, "store");
            var neighbor = path + ".pending";
            await File.WriteAllBytesAsync(neighbor, [7, 3]);
            OperationId operation = new(Guid.NewGuid());
            CollectionId collection = new(Guid.NewGuid());
            var start = new PageConformanceDriver().CreateCrashProcess(new(Encoding.UTF8.GetBytes(path)), checkpoint, operation, collection);
            await KillAtCheckpoint(start);
            var abandoned = Directory.GetFiles(directory, ".page-stage-*.pending").ToDictionary(p => p, File.ReadAllBytes);
            Assert.That(abandoned, Has.Count.EqualTo(1));
            await using var recovered = await new PageBackendFactory(_ => path).OpenAsync(new(new([1]), BackendOpenMode.OpenExisting));
            Assert.That((await recovered.ResolveAsync(operation)).Status, Is.EqualTo(expected));
            await using var before = await recovered.OpenSnapshotAsync();
            Assert.That(await before.ReadAsync(collection, new([1])), Is.Null);
            Assert.That((await recovered.CommitAsync(new(new(Guid.NewGuid()), recovered.Id, before.Generation,
                [new(collection, new([1]), new([99]))]))).Status, Is.EqualTo(CommitStatus.Committed));
            await using var after = await recovered.OpenSnapshotAsync();
            Assert.That((await after.ReadAsync(collection, new([1])))!.ToArray(), Is.EqualTo(new byte[] { 99 }));
            Assert.That(await File.ReadAllBytesAsync(neighbor), Is.EqualTo(new byte[] { 7, 3 }));
            foreach (var pair in abandoned) Assert.That(await File.ReadAllBytesAsync(pair.Key), Is.EqualTo(pair.Value));
            Assert.That(Directory.GetFiles(directory, ".page-stage-*.pending"), Is.EquivalentTo(abandoned.Keys));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task KilledDirectoryCreatorDoesNotPublishAndRetryMakesTheAncestryDurable()
    {
        var directory = Directory.CreateTempSubdirectory("page-directory-crash-").FullName;
        try
        {
            var path = Path.Combine(directory, "first", "second", "store");
            var start = new PageConformanceDriver().CreateCrashProcess(new(Encoding.UTF8.GetBytes(path)),
                BackendTestCheckpoint.AdmissionWritten, new(Guid.NewGuid()), new(Guid.NewGuid()));
            start.ArgumentList[3] = "directory-flushed";
            await KillAtCheckpoint(start);
            Assert.That(Directory.Exists(Path.GetDirectoryName(path)), Is.True);
            Assert.That(Directory.GetFiles(directory, "*", SearchOption.AllDirectories), Is.Empty);
            Assert.ThrowsAsync<IOException>(async () => await new PageBackendFactory(_ => path)
                .OpenAsync(new(new([1]), BackendOpenMode.OpenExisting)));
            var synchronized = new List<string>();
            StoreId id;
            await using (var store = await new PageBackendFactory(_ => path) { DirectoryFlushed = synchronized.Add }
                             .OpenAsync(new(new([1]), BackendOpenMode.CreateNew))) id = store.Id;
            Assert.That(synchronized, Does.Contain(directory));
            Assert.That(synchronized[^1], Is.EqualTo(Path.GetPathRoot(directory)));
            await using var reopened = await new PageBackendFactory(_ => path).OpenAsync(new(new([1]), BackendOpenMode.OpenExisting));
            Assert.That(reopened.Id, Is.EqualTo(id));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static async Task KillAtCheckpoint(ProcessStartInfo start)
    {
        start.UseShellExecute = false; start.RedirectStandardOutput = true; start.RedirectStandardError = true;
        using (var process = Process.Start(start)!)
        {
            var errors = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
            try
            {
                var marker = new char[11];
                await process.StandardOutput.ReadBlockAsync(marker.AsMemory()).AsTask().WaitAsync(TimeSpan.FromSeconds(30));
                Assert.That(new string(marker), Is.EqualTo("CHECKPOINT\n"));
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
                await errors;
            }
        }
    }
}
