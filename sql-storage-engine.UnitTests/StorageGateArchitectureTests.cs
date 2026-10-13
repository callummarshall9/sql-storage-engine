using AwesomeAssertions;
using sql_storage_engine.Exposures.Access;

namespace sql_storage_engine.UnitTests;

public sealed class StorageGateArchitectureTests
{
    [Test]
    public async Task ScopeDrainIsSharedAndDisposesRegisteredResourcesOnce()
    {
        using var gate = new StorageGateExposure();
        using var lease = await gate.EnterAsync(CancellationToken.None);
        using var activation = lease.Activate();
        var resource = new Resource();
        gate.RegisterScope(gate.ReadCurrent()!, resource);
        var first = activation.DrainAsync();
        var second = activation.DrainAsync();
        first.Should().BeSameAs(second);
        await first;
        resource.Disposals.Should().Be(1);
        ((Action)(() => gate.RegisterScope(gate.ReadCurrent()!, new Resource()))).Should().Throw<InvalidOperationException>();
    }

    [Test]
    public async Task ChildDrainWaitsForLeaseAndRejectsConcurrentSibling()
    {
        using var gate = new StorageGateExposure();
        using var lease = await gate.EnterAsync(CancellationToken.None);
        using var activation = lease.Activate();
        var child = await gate.EnterAsync(CancellationToken.None);
        await ((Func<Task>)(async () => await gate.EnterAsync(CancellationToken.None))).Should().ThrowAsync<InvalidOperationException>();
        var draining = activation.DrainAsync();
        draining.IsCompleted.Should().BeFalse();
        child.Dispose();
        await draining.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task ReleasedLeaseCannotActivateAndCancelledWaitKeepsGateAvailable()
    {
        using var gate = new StorageGateExposure();
        var lease = await gate.EnterAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var waiting = gate.EnterAsync(cancellation.Token, reentrant: false).AsTask();
        cancellation.Cancel();
        var failure = await ((Func<Task>)(async () => await waiting)).Should().ThrowAsync<OperationCanceledException>();
        failure.Which.CancellationToken.Should().Be(cancellation.Token);
        lease.Dispose();
        ((Action)(() => lease.Activate())).Should().Throw<ObjectDisposedException>();
        await ((Func<Task>)(async () => await lease.EnterAsync(CancellationToken.None))).Should().ThrowAsync<ObjectDisposedException>();
        using var next = await gate.EnterAsync(CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    private sealed class Resource : IAsyncDisposable
    {
        public int Disposals { get; private set; }
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }
}