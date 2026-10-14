// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Services.Managements.EngineResources;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Services.Orchestrations.OverflowChains;

namespace sql_storage_engine.UnitTests;
public sealed class EngineResourceArchitectureTests
{
    [Test]
    public async Task OpenedResourceGraphSharesOverflowPagesAndReleasesAllPins()
    {
        var directory = Path.Combine(Path.GetTempPath(), "engine-resources-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var resources = EngineResourceContextManagementService.CreateEngineResourceContextManagementService();
        EngineResourceContext? opened = null;
        try
        {
            opened = await resources.CreateEngineResourceContextAsync(new EngineResourceContext { Path = Path.Combine(directory, "database.db"), PageSize = 8192, BufferCapacity = 4 }, CancellationToken.None);
            opened.Header.Should().NotBeNull();
            opened.DatabaseBinding.Should().NotBeNull();
            opened.CatalogStorage.Should().NotBeNull();
            var overflows = OverflowChainContextOrchestrationService.CreateOverflowChainContextOrchestrationService(opened.OverflowConfiguration!);
            var payload = new byte[12000];
            new Random(42).NextBytes(payload);
            var encoded = await overflows.WriteOverflowChainContextAsync(new OverflowChainContext { Value = payload }, CancellationToken.None);
            encoded.Reference.Should().NotBeNull();
            var decoded = await overflows.ReadOverflowChainContextAsync(new OverflowChainContext { Reference = encoded.Reference }, CancellationToken.None);
            decoded.Value.ToArray().Should().Equal(payload);
            await overflows.FreeOverflowChainContextAsync(new OverflowChainContext { Reference = encoded.Reference }, CancellationToken.None);
            await resources.FlushBufferEngineResourceContextAsync(opened, CancellationToken.None);
            await resources.DiscardBufferEngineResourceContextAsync(opened, CancellationToken.None);
        }
        finally
        {
            if (opened is not null) await resources.CloseEngineResourceContextAsync(opened, CancellationToken.None);
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task GateLifetimesRestoreTheParentScopeAndReleaseReentrantChildren()
    {
        var resources = EngineResourceContextManagementService.CreateEngineResourceContextManagementService();
        var entered = await resources.EnterEngineResourceContextAsync(new EngineResourceContext { Reentrant = true }, CancellationToken.None);
        using (entered.Lifetime)
        {
            var activated = resources.ActivateEngineResourceContext(entered);
            using (activated.Lifetime)
            {
                resources.ReadEngineResourceContextAccess(new EngineResourceContext()).Access.Should().NotBeNull();
                var child = resources.EnterImmediateEngineResourceContext(new EngineResourceContext());
                child.Lifetime!.Dispose();
                await resources.DrainEngineResourceContextAsync(activated, CancellationToken.None);
            }
            resources.ReadEngineResourceContextAccess(new EngineResourceContext()).Access.Should().BeNull();
        }
        var next = await resources.EnterEngineResourceContextAsync(new EngineResourceContext { Reentrant = false }, CancellationToken.None);
        next.Lifetime!.Dispose();
    }

    [Test]
    public async Task DrainDisposesEveryResourceAndWaitsForChildrenAfterCleanupFailure()
    {
        var resources = EngineResourceContextManagementService.CreateEngineResourceContextManagementService();
        var entered = await resources.EnterEngineResourceContextAsync(new EngineResourceContext { Reentrant = true }, CancellationToken.None);
        using (entered.Lifetime)
        {
            var active = resources.ActivateEngineResourceContext(entered);
            using (active.Lifetime)
            {
                var first = new CleanupResource(true);
                var second = new CleanupResource(false);
                resources.RegisterScopeEngineResourceContext(active with { ScopeResource = first });
                resources.RegisterScopeEngineResourceContext(active with { ScopeResource = second });
                var child = resources.EnterImmediateEngineResourceContext(new EngineResourceContext());
                var draining = resources.DrainEngineResourceContextAsync(active, CancellationToken.None).AsTask();
                first.Disposed.Should().BeTrue();
                second.Disposed.Should().BeTrue();
                draining.IsCompleted.Should().BeFalse();
                child.Lifetime!.Dispose();
                Func<Task> cleanup = () => draining;
                await cleanup.Should().ThrowAsync<Exception>();
            }
        }
        var next = await resources.EnterEngineResourceContextAsync(new EngineResourceContext(), CancellationToken.None);
        next.Lifetime!.Dispose();
    }

    [Test]
    public async Task TransactionRuntimeRejectsParallelOperationsAndLinksCallerCancellation()
    {
        var resources = EngineResourceContextManagementService.CreateEngineResourceContextManagementService();
        var runtime = resources.CreateTransactionRuntimeEngineResourceContext(new EngineResourceContext { ExpiryNotification = () => { }, Timeout = TimeSpan.FromMinutes(1) });
        try
        {
            resources.EnterTransactionOperationEngineResourceContext(runtime);
            Action parallel = () => resources.EnterTransactionOperationEngineResourceContext(runtime);
            parallel.Should().Throw<InvalidOperationException>();
            var pending = resources.WaitTransactionOperationEngineResourceContextAsync(runtime, CancellationToken.None).AsTask();
            pending.IsCompleted.Should().BeFalse();
            resources.ReleaseTransactionOperationEngineResourceContext(runtime);
            await pending;
            resources.ReleaseTransactionOperationEngineResourceContext(runtime);
            using var caller = new CancellationTokenSource();
            var linked = resources.LinkTransactionTokenEngineResourceContext(runtime with { InputToken = caller.Token });
            using (linked.Lifetime)
            {
                caller.Cancel();
                linked.TokenResult.IsCancellationRequested.Should().BeTrue();
                runtime.TokenResult.IsCancellationRequested.Should().BeFalse();
            }
        }
        finally
        {
            resources.CloseTransactionRuntimeEngineResourceContext(runtime);
        }
    }

    [Test]
    public async Task TransactionRuntimeSignalsExpiryAndCancelsItsDeadline()
    {
        var resources = EngineResourceContextManagementService.CreateEngineResourceContextManagementService();
        var expired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = resources.CreateTransactionRuntimeEngineResourceContext(new EngineResourceContext { ExpiryNotification = () => expired.TrySetResult(), Timeout = TimeSpan.FromMilliseconds(20) });
        try
        {
            runtime.TokenResult.IsCancellationRequested.Should().BeFalse();
            resources.StartTransactionRuntimeEngineResourceContext(runtime);
            await expired.Task.WaitAsync(TimeSpan.FromSeconds(5));
            runtime.TokenResult.IsCancellationRequested.Should().BeTrue();
        }
        finally
        {
            resources.CloseTransactionRuntimeEngineResourceContext(runtime);
        }
    }

    [Test]
    public async Task WaitingForACallbackPreservesItsOriginalFailure()
    {
        var resources = EngineResourceContextManagementService.CreateEngineResourceContextManagementService();
        var failure = new IOException("Callback resource failed.");
        var context = new EngineResourceContext { TaskSource = Task.FromException(failure), Timeout = Timeout.InfiniteTimeSpan };
        Func<Task> wait = () => resources.WaitTransactionTaskEngineResourceContextAsync(context, CancellationToken.None).AsTask();
        (await wait.Should().ThrowAsync<IOException>()).Which.Should().BeSameAs(failure);
    }

    private sealed class CleanupResource(bool fail) : IAsyncDisposable
    {
        public bool Disposed { get; private set; }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            if (fail) throw new InvalidOperationException("Resource disposal failed.");
            return ValueTask.CompletedTask;
        }
    }
}
