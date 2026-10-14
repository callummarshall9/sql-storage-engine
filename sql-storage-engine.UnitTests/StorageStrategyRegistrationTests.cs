using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using sql_storage_engine.Exposures.Strategies;
using sql_storage_engine.Exposures.Buffers;
using sql_storage_engine.Exposures.Heap;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class StorageStrategyRegistrationTests
{
    [Test]
    public void LastStrategySelectionReplacesThePreviousSingleton()
    {
        var services = new ServiceCollection();
        var first = Path.Combine(Path.GetTempPath(), "storage-registration-first");
        var second = Path.Combine(Path.GetTempPath(), "storage-registration-second");
        services.AddSqlStorageEngine().UseLocalFileSystem(first).UseLocalFileSystem(second);
        services.Count(descriptor => descriptor.ServiceType == typeof(IStorageStrategyExposure)).Should().Be(1);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IStorageStrategyExposure>().Should().BeOfType<FileStorageStrategyExposure>();
        provider.GetRequiredService<IStorageStrategyExposure>().Should().BeSameAs(provider.GetRequiredService<IStorageStrategyExposure>());
    }

    [Test]
    public async Task HostConfigurationControlsRemoteSegmentGeometryWithoutAFileDirectory()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["StorageEngine:DefaultOptions:SegmentSize"] = "16384",
            ["StorageEngine:DefaultOptions:MaximumCacheBytes"] = "65536"
        }).Build();
        var store = new RecordingObjectStore();
        var services = new ServiceCollection();
        services.AddSqlStorageEngine(configuration, adjustment: storageEngineConfiguration =>
        {
            storageEngineConfiguration.DefaultOptions!.SegmentSize.Should().Be(16384);
            storageEngineConfiguration.DefaultOptions = storageEngineConfiguration.DefaultOptions with { SegmentSize = 8192 };
        }).UseObjectStorage(store);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IStorageObjectStoreExposure>().Should().BeSameAs(store);
        var strategy = provider.GetRequiredService<IStorageStrategyExposure>();
        await using var database = await strategy.CreateStorageDatabaseExposureAsync("configuration", pageSize: 4096);
        await database.AllocateAsync(PageType.Heap);
        await database.AllocateAsync(PageType.Heap);
        await database.AllocateAsync(PageType.Heap);
        await database.CommitAsync();
        store.UploadLengths.Order().Should().Equal(8192, 8192);
    }

    [Test]
    public async Task FileStrategyUsesExistingOpaqueHeapAndReopensDurably()
    {
        var directory = Path.Combine(Path.GetTempPath(), "storage-strategy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var services = new ServiceCollection();
            services.AddSqlStorageEngine().UseLocalFileSystem(directory);
            using var provider = services.BuildServiceProvider();
            var strategy = provider.GetRequiredService<IStorageStrategyExposure>();
            RowId rowId;
            PageId heapRoot;
            await using (var database = await strategy.CreateStorageDatabaseExposureAsync("records"))
            {
                await using var pool = new BufferPoolExposure(database, 8, leaveOpen: true);
                var heap = await TableHeapExposure.CreateTableHeapExposureAsync(pool, database);
                rowId = await heap.InsertAsync("opaque payload"u8.ToArray());
                heapRoot = heap.ReadRootPageId();
                await pool.FlushAllAsync();
                await database.PublishCatalogRootAsync(heapRoot);
                await database.CommitAsync();
            }
            await using var reopened = await strategy.OpenAsync("records");
            reopened.ReadHeader().CatalogRootPageId.Should().Be(heapRoot);
            await using var reopenedPool = new BufferPoolExposure(reopened, 8, leaveOpen: true);
            var reopenedHeap = await TableHeapExposure.OpenAsync(heapRoot, reopenedPool, reopened);
            var row = await reopenedHeap.ReadAsync(rowId);
            row.Row.ToArray().Should().Equal("opaque payload"u8.ToArray());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class RecordingObjectStore : IStorageObjectStoreExposure
    {
        public List<long> UploadLengths { get; } = [];
        public ValueTask<(byte[] Bytes, string Version)?> ReadMetadataAsync(string key, int maximumBytes, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<(byte[] Bytes, string Version)?>(null);

        public ValueTask DownloadAsync(string key, Stream destination, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("This fixture never downloads.");

        public ValueTask UploadAsync(string key, Stream source, CancellationToken cancellationToken = default)
        {
            lock (UploadLengths) UploadLengths.Add(source.Length);
            return ValueTask.CompletedTask;
        }

        public ValueTask<string> PublishMetadataAsync(string key, byte[] bytes, string? expectedVersion, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult("version-1");
    }

}