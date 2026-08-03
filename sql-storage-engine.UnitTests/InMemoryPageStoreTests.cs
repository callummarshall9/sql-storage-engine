using AwesomeAssertions;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class InMemoryPageStoreTests
{
    [Test]
    public async Task Store_CopiesBuffersAndReusesFreedPages()
    {
        await using var store = new InMemoryPageStore();
        var id = await store.AllocateAsync(PageType.Heap);
        var source = Enumerable.Repeat((byte)7, store.PageSize).ToArray();
        await store.WriteAsync(id, source); source[0] = 9;
        var read = new byte[store.PageSize];
        await store.ReadAsync(id, read); read[1] = 9;
        var secondRead = new byte[store.PageSize];
        await store.ReadAsync(id, secondRead);
        secondRead[0].Should().Be(7); secondRead[1].Should().Be(7);
        await store.FreeAsync(id);
        await ((Func<Task>)(async () => await store.ReadAsync(id, read))).Should().ThrowAsync<StorageResourceException>();
        (await store.AllocateAsync(PageType.Catalog)).Should().Be(id);
    }

    [Test]
    public async Task Store_RejectsWrongBuffersDoubleFreeCancellationAndUseAfterDispose()
    {
        var store = new InMemoryPageStore();
        var id = await store.AllocateAsync(PageType.Heap);
        await ((Func<Task>)(async () => await store.WriteAsync(id, new byte[1]))).Should().ThrowAsync<ArgumentException>();
        await store.FreeAsync(id);
        await ((Func<Task>)(async () => await store.FreeAsync(id))).Should().ThrowAsync<StorageResourceException>();
        var cancelled = new CancellationToken(true);
        await ((Func<Task>)(async () => await store.FlushAsync(cancelled))).Should().ThrowAsync<OperationCanceledException>();
        await store.DisposeAsync();
        await ((Func<Task>)(async () => await store.FlushAsync())).Should().ThrowAsync<ObjectDisposedException>();
    }

    [TestCase(FaultInjectingPageStore.Operation.Read)]
    [TestCase(FaultInjectingPageStore.Operation.Write)]
    [TestCase(FaultInjectingPageStore.Operation.Flush)]
    [TestCase(FaultInjectingPageStore.Operation.Allocate)]
    public async Task Decorator_InjectsEachPageStoreFailure(FaultInjectingPageStore.Operation operation)
    {
        await using var inner = new InMemoryPageStore();
        var id = await inner.AllocateAsync(PageType.Heap);
        await using var store = new FaultInjectingPageStore(inner, inner) { FailOn = operation };
        Func<Task> action = operation switch
        {
            FaultInjectingPageStore.Operation.Read => async () => await store.ReadAsync(id, new byte[store.PageSize]),
            FaultInjectingPageStore.Operation.Write => async () => await store.WriteAsync(id, new byte[store.PageSize]),
            FaultInjectingPageStore.Operation.Flush => async () => await store.FlushAsync(),
            _ => async () => await store.AllocateAsync(PageType.Heap)
        };
        await action.Should().ThrowAsync<IOException>();
    }
}
