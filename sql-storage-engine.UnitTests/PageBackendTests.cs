using SqlExecutionEngine.Storage.Abstractions;
using SqlExecutionEngine.Storage.Conformance;
using SqlStorageEngine.Core;

namespace sql_storage_engine.UnitTests;

public sealed class PageBackendTests
{
    [Test]
    public async Task UnchangedSharedConformancePassesAllNineChecks()
    {
        var report = await BackendConformanceRunner.RunAsync([PageConformanceDriver.Registration()]);
        Assert.That(report.Results, Has.Count.EqualTo(9));
        Assert.That(report.Passed, Is.True, report.ToJson());
    }

    [Test]
    public async Task CorruptionAndSqlFilesFailClosedWithoutChangingFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "store");
        var factory = new PageBackendFactory(_ => path);
        try
        {
            await using (var store = await factory.OpenAsync(new(new([1]), BackendOpenMode.CreateNew))) { }
            var bytes = await File.ReadAllBytesAsync(path);
            bytes[^1] ^= 1;
            await File.WriteAllBytesAsync(path, bytes);
            Assert.ThrowsAsync<IOException>(async () => await factory.OpenAsync(new(new([1]), BackendOpenMode.OpenExisting)));
            Assert.That(await File.ReadAllBytesAsync(path), Is.EqualTo(bytes));
            File.Delete(path);
            await using (var sql = await StorageEngine.CreateAsync(path)) { }
            bytes = await File.ReadAllBytesAsync(path);
            Assert.ThrowsAsync<IOException>(async () => await factory.OpenAsync(new(new([1]), BackendOpenMode.OpenExisting)));
            Assert.That(await File.ReadAllBytesAsync(path), Is.EqualTo(bytes));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task WriterLeaseAndExistenceModesPreventReplacement()
    {
        await using var location = await PageConformanceDriver.Registration().CreateLocation(default);
        var factory = PageConformanceDriver.Registration().Factory;
        await using var store = await factory.OpenAsync(new(location.Location, BackendOpenMode.CreateNew));
        Assert.ThrowsAsync<IOException>(async () => await factory.OpenAsync(new(location.Location, BackendOpenMode.OpenExisting)));
        Assert.ThrowsAsync<IOException>(async () => await factory.OpenAsync(new(location.Location, BackendOpenMode.CreateNew)));
    }
}
