using AwesomeAssertions;

namespace sql_storage_engine.UnitTests;

public sealed class ExplicitTransactionPendingCallbackTests
{
    [Test]
    public async Task NonCooperativeCallbackBoundsCallerWaitButRetainsFileOwnershipUntilCleanup()
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        var transaction = await database.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(10)));
        using var cancel = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var execution = transaction.ExecuteStatementAsync(async (_, _) => { entered.SetResult(); await release.Task; }, cancel.Token).AsTask();
        await entered.Task; cancel.Cancel();
        try
        {
            Func<Task> execute = async () => await execution.WaitAsync(TimeSpan.FromSeconds(5));
            await execute.Should().ThrowAsync<OperationCanceledException>();
            transaction.State.Should().Be(StorageTransactionState.Indeterminate);
            Func<Task> dispose = async () => await database.Engine.DisposeAsync();
            await dispose.Should().ThrowAsync<InvalidOperationException>();
            Func<Task> open = async () => await StorageEngine.OpenAsync(database.Path);
            await open.Should().ThrowAsync<IOException>();
        }
        finally { release.TrySetResult(); }
        await database.Engine.PendingTransactionCleanup!.WaitAsync(TimeSpan.FromSeconds(5));
        await database.ReopenAsync();
        (await database.Engine.ResolveTransactionAsync(transaction.Identity)).State.Should().Be(StorageTransactionState.Aborted);
        await transaction.DisposeAsync();
    }
}
