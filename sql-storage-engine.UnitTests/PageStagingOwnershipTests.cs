using SqlExecutionEngine.Storage.Abstractions;
using SqlStorageEngine.Core;

namespace sql_storage_engine.UnitTests;

public sealed class PageStagingOwnershipTests
{
    private string directory = null!;
    private static readonly CollectionId Collection = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));

    [SetUp]
    public void SetUp() => directory = Directory.CreateTempSubdirectory("page-staging-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(directory, true);

    [Test]
    public async Task ValidStoreNamedPendingSurvivesNeighborCreationPublicationAndReopen()
    {
        var path = Path.Combine(directory, "store");
        var neighbor = path + ".pending";
        await CreateWithValue(neighbor, 73);
        var original = await File.ReadAllBytesAsync(neighbor);
        await CreateWithValue(path, 42);
        await AssertValue(path, 42);
        await AssertValue(neighbor, 73);
        Assert.That(await File.ReadAllBytesAsync(neighbor), Is.EqualTo(original));
        Assert.That(Directory.GetFiles(directory, ".page-stage-*.pending"), Is.Empty);
    }

    [Test]
    public async Task LongValidStoreFileNameDoesNotNeedSpaceForAStagingSuffix()
    {
        var path = Path.Combine(directory, new string('s', 240));
        await CreateWithValue(path, 42);
        await AssertValue(path, 42);
        Assert.That(Directory.GetFiles(directory, ".page-stage-*.pending"), Is.Empty);
    }

    [Test]
    public async Task RejectedForeignOpenPreservesEveryNeighbor()
    {
        var path = Path.Combine(directory, "foreign");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        await CreateWithValue(path + ".pending", 73);
        var namedLikeStage = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        await File.WriteAllBytesAsync(namedLikeStage, [9, 8, 7]);
        var originals = Directory.GetFiles(directory).ToDictionary(p => p, File.ReadAllBytes);
        Assert.ThrowsAsync<InvalidDataException>(async () => await Factory(path).OpenAsync(Request(BackendOpenMode.OpenExisting)));
        foreach (var pair in originals) Assert.That(await File.ReadAllBytesAsync(pair.Key), Is.EqualTo(pair.Value), pair.Key);
        await AssertValue(path + ".pending", 73);
    }

    [Test]
    public async Task FailedPublicationCleansItsClaimedStageButPreservesNeighbors()
    {
        var path = Path.Combine(directory, "store");
        await CreateWithValue(path, 42);
        var neighbor = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        await File.WriteAllBytesAsync(neighbor, [7, 3]);
        OperationId operation = new(Guid.NewGuid());
        var factory = new PageBackendFactory(_ => path)
        {
            Checkpoint = stage => stage == "publication-written" ? ValueTask.FromException(new IOException("Injected failure.")) : ValueTask.CompletedTask
        };
        await using (var store = await factory.OpenAsync(Request(BackendOpenMode.OpenExisting)))
        {
            await using var snapshot = await store.OpenSnapshotAsync();
            Assert.That((await store.CommitAsync(new(operation, store.Id, snapshot.Generation,
                [new(Collection, new([1]), new([99]))]))).Status, Is.EqualTo(CommitStatus.Unknown));
        }
        Assert.That(Directory.GetFiles(directory, ".page-stage-*.pending"), Is.Empty);
        Assert.That(File.Exists(neighbor), Is.True);
        await using var recovered = await Factory(path).OpenAsync(Request(BackendOpenMode.OpenExisting));
        Assert.That((await recovered.ResolveAsync(operation)).Status, Is.EqualTo(CommitStatus.Conflict));
        await using var read = await recovered.OpenSnapshotAsync();
        Assert.That((await read.ReadAsync(Collection, new([1])))!.ToArray(), Is.EqualTo(new byte[] { 42 }));
        Assert.That(await File.ReadAllBytesAsync(neighbor), Is.EqualTo(new byte[] { 7, 3 }));
    }

    private static PageBackendFactory Factory(string path) => new(_ => path);
    private static BackendOpenRequest Request(BackendOpenMode mode) => new(new([1]), mode);

    private static async Task CreateWithValue(string path, byte value)
    {
        await using var store = await Factory(path).OpenAsync(Request(BackendOpenMode.CreateNew));
        await using var snapshot = await store.OpenSnapshotAsync();
        Assert.That((await store.CommitAsync(new(new(Guid.NewGuid()), store.Id, snapshot.Generation,
            [new(Collection, new([1]), new([value]))]))).Status, Is.EqualTo(CommitStatus.Committed));
    }

    private static async Task AssertValue(string path, byte value)
    {
        await using var store = await Factory(path).OpenAsync(Request(BackendOpenMode.OpenExisting));
        await using var snapshot = await store.OpenSnapshotAsync();
        Assert.That((await snapshot.ReadAsync(Collection, new([1])))!.ToArray(), Is.EqualTo(new byte[] { value }));
    }
}
