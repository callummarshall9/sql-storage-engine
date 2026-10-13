using AwesomeAssertions;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class PageStoreArchitectureTests
{
    [Test]
    public async Task FileStorePreservesOffsetsGapsAndShortReadEvidence()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pages");
        try
        {
            await using var store = FilePageStoreExposure.OpenNewFilePageStore(path, PageConstants.DefaultSize);
            var source = Enumerable.Range(0, store.GetPageSize()).Select(value => (byte)value).ToArray();
            await store.WriteAsync(new PageId { Value = 2 }, source);
            await store.FlushAsync();
            var read = new byte[store.GetPageSize()];
            await store.ReadAsync(new PageId { Value = 2 },read);
            read.Should().Equal(source);
            await store.ReadAsync(new PageId { Value = 0 },read);
            read.Should().OnlyContain(value => value == 0);
            var error = (await ((Func<Task>)(async () => await store.ReadAsync(new PageId { Value = 3 },read)))
                .Should().ThrowAsync<StorageFormatException>()).Which;
            error.GetBaseException().Should().BeOfType<EndOfStreamException>();
        }
        finally { File.Delete(path); }
    }

    [Test]
    public async Task FileStoreValidatesBeforeWritesAndPreservesCancellationAndOverflow()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pages");
        try
        {
            await using var store = FilePageStoreExposure.OpenNewFilePageStore(path, PageConstants.DefaultSize);
            await ((Func<Task>)(async () => await store.WriteAsync(default,new byte[1])))
                .Should().ThrowAsync<StorageModelValidationException>();
            new FileInfo(path).Length.Should().Be(0);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var error = (await ((Func<Task>)(async () => await store.WriteAsync(default,
                new byte[store.GetPageSize()], cancellation.Token))).Should().ThrowAsync<OperationCanceledException>()).Which;
            error.CancellationToken.Should().Be(cancellation.Token);
            new FileInfo(path).Length.Should().Be(0);
            await ((Func<Task>)(async () => await store.ReadAsync(new PageId { Value = ulong.MaxValue },                new byte[store.GetPageSize()]))).Should().ThrowAsync<OverflowException>();
        }
        finally { File.Delete(path); }
    }

    [Test]
    public async Task ReadOnlyStoreSharesWithWriterAndRejectsWriteThroughTheResourceBoundary()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pages");
        try
        {
            await using var writer = FilePageStoreExposure.OpenNewFilePageStore(path, PageConstants.DefaultSize);
            var bytes = Enumerable.Repeat((byte)42, writer.GetPageSize()).ToArray();
            await writer.WriteAsync(default, bytes);
            await using var reader = FilePageStoreExposure.OpenExistingFilePageStore(path, writer.GetPageSize(), true);
            var read = new byte[reader.GetPageSize()];
            await reader.ReadAsync(default,read);
            read.Should().Equal(bytes);
            await ((Func<Task>)(async () => await reader.WriteAsync(default, bytes)))
                .Should().ThrowAsync<StorageResourceException>();
        }
        finally { File.Delete(path); }
    }

    [Test]
    public async Task MemoryStoreCopiesBuffersReusesOwnershipAndInitializesValidHeaders()
    {
        await using var store = new MemoryPageStoreExposure();
        var id = await store.AllocateAsync(PageType.Heap);
        var source = Enumerable.Repeat((byte)42, store.GetPageSize()).ToArray();
        await store.WriteAsync(id, source);
        source[0] = 0;
        var read = new byte[store.GetPageSize()];
        await store.ReadAsync(id,read);
        read[0].Should().Be(42);
        read[0] = 0;
        await store.ReadAsync(id,read);
        read[0].Should().Be(42);
        await store.FreeAsync(id);
        await ((Func<Task>)(async () => await store.FreeAsync(id))).Should().ThrowAsync<StorageResourceException>();
        var reused = await store.AllocateAsync(PageType.Overflow);
        reused.Should().Be(id);
        await store.ReadAsync(reused,read);
        PageChecksumExposure.ValidateChecksum(read,store.GetPageSize());
        PageHeaderExposure.CheckPageHeader(PageHeaderExposure.ReadPageHeader(read),id.Value,PageType.Overflow);
    }

    [Test]
    public async Task ClosingStoresIsIdempotentAndKeepsMetadataReadable()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pages");
        try
        {
            var store = FilePageStoreExposure.OpenNewFilePageStore(path, PageConstants.DefaultSize);
            await store.DisposeAsync();
            await store.DisposeAsync();
            store.GetPageSize().Should().Be(PageConstants.DefaultSize);
            await ((Func<Task>)(async () => await store.ReadAsync(default,new byte[store.GetPageSize()])))
                .Should().ThrowAsync<ObjectDisposedException>();
            var memory = new MemoryPageStoreExposure(reservePageZero: false);
            (await memory.AllocateAsync(PageType.DatabaseHeader)).Value.Should().Be(0);
            await memory.DisposeAsync();
            await memory.DisposeAsync();
            memory.GetPageSize().Should().Be(PageConstants.DefaultSize);
            await ((Func<Task>)(async () => await memory.AllocateAsync(PageType.Heap)))
                .Should().ThrowAsync<ObjectDisposedException>();
        }
        finally { File.Delete(path); }
    }
}