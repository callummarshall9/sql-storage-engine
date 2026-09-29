using SqlExecutionEngine.Storage.Abstractions;
using SqlStorageEngine.Core;

namespace sql_storage_engine.UnitTests;

public sealed class PageDirectoryDurabilityTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task EveryAncestorIsSynchronizedBeforeReturningCreateEvenWhenAlreadyPresent(bool preexisting)
    {
        var root = Directory.CreateTempSubdirectory("page-directory-").FullName;
        try
        {
            var leaf = Path.Combine(root, "first", "second", "third");
            var path = Path.Combine(leaf, "store");
            if (preexisting) Directory.CreateDirectory(leaf);
            var synchronized = new List<string>();
            var factory = new PageBackendFactory(_ => path)
            {
                DirectoryFlushed = directory =>
                {
                    Assert.That(File.Exists(path), Is.False, "No store may be published before its ancestry is durable.");
                    synchronized.Add(directory);
                }
            };
            await using (var store = await factory.OpenAsync(new(new([1]), BackendOpenMode.CreateNew))) { }
            var expected = new List<string>();
            for (var parent = Directory.GetParent(leaf); parent is not null; parent = parent.Parent) expected.Add(parent.FullName);
            Assert.That(synchronized, Is.EqualTo(expected));
            await using var reopened = await new PageBackendFactory(_ => path).OpenAsync(new(new([1]), BackendOpenMode.OpenExisting));
            await using var snapshot = await reopened.OpenSnapshotAsync();
            Assert.That(snapshot.Store, Is.EqualTo(reopened.Id));
        }
        finally { Directory.Delete(root, true); }
    }

    [Test]
    public async Task FailureWhileSynchronizingAncestorsNeverPublishesAStoreAndRetryResynchronizes()
    {
        var root = Directory.CreateTempSubdirectory("page-directory-").FullName;
        try
        {
            var path = Path.Combine(root, "first", "second", "store");
            var factory = new PageBackendFactory(_ => path)
            {
                DirectoryFlushed = _ => throw new IOException("Injected ancestry synchronization interruption.")
            };
            Assert.ThrowsAsync<IOException>(async () => await factory.OpenAsync(new(new([1]), BackendOpenMode.CreateNew)));
            Assert.That(Directory.GetFiles(root, "*", SearchOption.AllDirectories), Is.Empty);
            var synchronized = new List<string>();
            await using var store = await new PageBackendFactory(_ => path) { DirectoryFlushed = synchronized.Add }
                .OpenAsync(new(new([1]), BackendOpenMode.CreateNew));
            Assert.That(synchronized, Does.Contain(root));
            Assert.That(synchronized[^1], Is.EqualTo(Path.GetPathRoot(root)));
        }
        finally { Directory.Delete(root, true); }
    }
}
