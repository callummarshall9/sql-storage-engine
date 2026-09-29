using System.Diagnostics;
using SqlExecutionEngine.Storage.Abstractions;

namespace sql_storage_engine.UnitTests;

public sealed class PageBackendResourceTests
{
    [Test]
    public async Task ExactStoreCapacitySurvivesReopenAndOneOverHasNoReceipt()
    {
        var registration = PageConformanceDriver.Registration();
        await using var location = await registration.CreateLocation(default);
        var watch = Stopwatch.StartNew();
        var collection = new CollectionId(Guid.NewGuid());
        OperationId rejected;
        await using (var store = await registration.Factory.OpenAsync(new(location.Location, BackendOpenMode.CreateNew)))
        {
            for (var start = 0; start < 8; start += 3)
            {
                await using var snapshot = await store.OpenSnapshotAsync();
                var mutations = Enumerable.Range(start, Math.Min(3, 8 - start)).Select(i =>
                    new BackendMutation(collection, new([(byte)i]), new(new byte[1048576 - (i == 7 ? 208 : 0)]))).ToArray();
                Assert.That((await store.CommitAsync(new(new(Guid.NewGuid()), store.Id, snapshot.Generation, mutations))).Status, Is.EqualTo(CommitStatus.Committed));
            }
            await using var full = await store.OpenSnapshotAsync();
            rejected = new(Guid.NewGuid());
            var batch = new BackendBatch(rejected, store.Id, full.Generation,
                [new(collection, new([7]), new(new byte[1048576 - 207]))]);
            Assert.ThrowsAsync<BackendResourceException>(async () => await store.CommitAsync(batch));
            Assert.That((await store.ResolveAsync(rejected)).Status, Is.EqualTo(CommitStatus.NotSubmitted));
        }
        await using var reopened = await registration.Factory.OpenAsync(new(location.Location, BackendOpenMode.OpenExisting));
        await using var read = await reopened.OpenSnapshotAsync();
        for (var i = 0; i < 8; i++)
        {
            var bytes = await read.ReadAsync(collection, new([(byte)i]));
            Assert.That(bytes!.ToArray(), Is.EqualTo(new byte[1048576 - (i == 7 ? 208 : 0)]));
        }
        Assert.That((await reopened.ResolveAsync(rejected)).Status, Is.EqualTo(CommitStatus.NotSubmitted));
        var path = System.Text.Encoding.UTF8.GetString(location.Location.ToArray());
        Assert.That(Directory.GetFiles(Path.GetDirectoryName(path)!, ".page-stage-*.pending"), Is.Empty);
        TestContext.Out.WriteLine($"Exact 8388608 charged bytes, file {new FileInfo(path).Length} bytes, elapsed {watch.ElapsedMilliseconds} ms; no performance threshold.");
    }
}
