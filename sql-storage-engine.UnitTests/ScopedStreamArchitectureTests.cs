// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Services.Managements.EngineResources;

namespace sql_storage_engine.UnitTests;
public sealed class ScopedStreamArchitectureTests
{
    [Test]
    public async Task ScopeDrain_ClosesTrackedEnumeratorAndRejectsLaterEnumeration()
    {
        var service = EngineResourceContextManagementService.CreateEngineResourceContextManagementService();
        var lease = await service.EnterEngineResourceContextAsync(engineResourceContext: new EngineResourceContext(), cancellationToken: default);
        var scope = service.ActivateEngineResourceContext(engineResourceContext: lease);
        var disposed = false;

        async IAsyncEnumerable<int> Source()
        {
            try
            {
                yield return 1;
                await Task.Yield();
                yield return 2;
            }
            finally
            {
                disposed = true;
            }
        }

        try
        {
            var tracked = (IAsyncEnumerable<int>)service.TrackEngineResourceContextStream<int>(engineResourceContext: new EngineResourceContext { StreamSource = Source(), Access = scope.Access })
            .StreamSource!;

            await using var iterator = tracked.GetAsyncEnumerator();

            (await iterator.MoveNextAsync()).Should()
            .BeTrue();

            iterator.Current.Should()
            .Be(1);

            await service.DrainEngineResourceContextAsync(engineResourceContext: scope, cancellationToken: default);

            disposed.Should()
            .BeTrue();

            await ((Func<Task>)(async () => await iterator.MoveNextAsync())).Should()
            .ThrowAsync<InvalidOperationException>();

            ((Action)(() => tracked.GetAsyncEnumerator())).Should()
            .Throw<InvalidOperationException>();
        }
        finally
        {
            scope.Lifetime!.Dispose();
            lease.Lifetime!.Dispose();
        }
    }

    [Test]
    public async Task ScopeDrain_WaitsForInFlightMoveAndRejectsConcurrentMoves()
    {
        var service = EngineResourceContextManagementService.CreateEngineResourceContextManagementService();
        var lease = await service.EnterEngineResourceContextAsync(engineResourceContext: new EngineResourceContext(), cancellationToken: default);
        var scope = service.ActivateEngineResourceContext(engineResourceContext: lease);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async IAsyncEnumerable<int> Source()
        {
            started.SetResult();
            await release.Task;
            yield return 7;
        }

        try
        {
            var tracked = (IAsyncEnumerable<int>)service.TrackEngineResourceContextStream<int>(engineResourceContext: new EngineResourceContext { StreamSource = Source(), Access = scope.Access })
            .StreamSource!;

            await using var iterator = tracked.GetAsyncEnumerator();

            var move = iterator.MoveNextAsync()
            .AsTask();

            await started.Task;

            await ((Func<Task>)(async () => await iterator.MoveNextAsync())).Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("A stream operation is already active.");

            var drain = service.DrainEngineResourceContextAsync(engineResourceContext: scope, cancellationToken: default)
            .AsTask();

            drain.IsCompleted.Should()
            .BeFalse();

            release.SetResult();

            (await move).Should()
            .BeTrue();

            await drain;

            await ((Func<Task>)(async () => await iterator.MoveNextAsync())).Should()
            .ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            release.TrySetResult();
            scope.Lifetime!.Dispose();
            lease.Lifetime!.Dispose();
        }
    }

    [Test]
    public async Task ScopeDrain_ClosesEveryReaderWhenOneSourceDisposeFails()
    {
        var service = EngineResourceContextManagementService.CreateEngineResourceContextManagementService();
        var lease = await service.EnterEngineResourceContextAsync(engineResourceContext: new EngineResourceContext(), cancellationToken: default);
        var scope = service.ActivateEngineResourceContext(engineResourceContext: lease);
        var started = 0;
        var disposed = new HashSet<int>();

        async IAsyncEnumerable<int> Source()
        {
            var id = ++started;

            try
            {
                yield return id;
                await Task.Yield();
            }
            finally
            {
                disposed.Add(id);

                if (id == 1)
                {
                    throw new InvalidOperationException("Source cleanup failed.");
                }
            }
        }

        try
        {
            var tracked = (IAsyncEnumerable<int>)service.TrackEngineResourceContextStream<int>(engineResourceContext: new EngineResourceContext { StreamSource = Source(), Access = scope.Access })
            .StreamSource!;

            await using var first = tracked.GetAsyncEnumerator();
            await using var second = tracked.GetAsyncEnumerator();
            await first.MoveNextAsync();
            await second.MoveNextAsync();

            await ((Func<Task>)(async () => await service.DrainEngineResourceContextAsync(engineResourceContext: scope, cancellationToken: default))).Should()
            .ThrowAsync<Exception>();

            disposed.Should()
            .BeEquivalentTo(new[] { 1, 2 });
        }
        finally
        {
            scope.Lifetime!.Dispose();
            lease.Lifetime!.Dispose();
        }
    }
}