using System.Diagnostics;
using AwesomeAssertions;

namespace sql_storage_engine.UnitTests;

public sealed class SavepointResourceTests
{
    [Test]
    public async Task ExactCountAndAggregateImageQuotasRejectBeforeChangingTheRoot()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        await using (var transaction = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30))))
        {
            var timer = Stopwatch.StartNew();
            var first = await transaction.CreateSavepointAsync("point");
            for (var i = 1; i < StorageSavepoint.MaximumCount; i++) await transaction.CreateSavepointAsync("point");
            Func<Task> over = async () => await transaction.CreateSavepointAsync("one-over");
            await over.Should().ThrowAsync<InvalidOperationException>();
            var bytes = Directory.GetFiles(Path.GetDirectoryName(db.Path)!, "*.savepoint-*").Sum(path => new FileInfo(path).Length);
            TestContext.Out.WriteLine($"savepoints=32; retainedBytes={bytes}; elapsedMilliseconds={timer.Elapsed.TotalMilliseconds:F3}; no timing gate");
            await transaction.RollbackToSavepointAsync(first);
            await transaction.CreateSavepointAsync("capacity-recovered");
            await transaction.CommitAsync();
        }
        var exact = new FileInfo(db.Path).Length + 64;
        await using (var transaction = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30), maximumJournalBytes: exact)))
        {
            await transaction.CreateSavepointAsync("exact");
            Func<Task> over = async () => await transaction.CreateSavepointAsync("one-over");
            await over.Should().ThrowAsync<InvalidOperationException>();
            transaction.State.Should().Be(StorageTransactionState.Committable);
            await transaction.CommitAsync();
        }
        Directory.GetFiles(Path.GetDirectoryName(db.Path)!, "*.savepoint-*").Should().BeEmpty();
    }

    [Test]
    public async Task ReentrantAndOverlappingOperationsRejectAndPrecancelledCallsKeepTheRoot()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        var point = await transaction.CreateSavepointAsync("point");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Func<Task> create = async () => await transaction.CreateSavepointAsync("cancelled", cancelled.Token);
        await create.Should().ThrowAsync<OperationCanceledException>();
        Func<Task> rollback = async () => await transaction.RollbackToSavepointAsync(point, cancelled.Token);
        await rollback.Should().ThrowAsync<OperationCanceledException>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = transaction.ExecuteStatementAsync(async (_, token) =>
        {
            Func<Task> nested = async () => await transaction.CreateSavepointAsync("nested");
            await nested.Should().ThrowAsync<InvalidOperationException>();
            entered.SetResult();
            await release.Task.WaitAsync(token);
        }).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Func<Task> overlap = async () => await transaction.RollbackToSavepointAsync(point);
            await overlap.Should().ThrowAsync<InvalidOperationException>();
        }
        finally { release.SetResult(); await pending; }
        transaction.State.Should().Be(StorageTransactionState.Committable);
        await transaction.CommitAsync();
    }
}
