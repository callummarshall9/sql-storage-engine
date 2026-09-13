using AwesomeAssertions;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;

public sealed class ExplicitTransactionConcurrencyTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task RootReaderWaitsForEntireTransaction(bool commit)
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await database.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        await transaction.ExecuteStatementAsync(async (context, token) =>
            await (await context.OpenTableAsync(database.Table.Id, token)).UpdateAsync(database.First,
                new RowUpdate([new ColumnUpdate(1, SqlValue.Integer(75))]), token));
        var reader = database.BalancesAsync();
        reader.IsCompleted.Should().BeFalse();
        if (commit) await transaction.CommitAsync(); else await transaction.RollbackAsync();
        (await reader.WaitAsync(TimeSpan.FromSeconds(5))).Should().Equal(commit ? [75L, 100L] : [100L, 100L]);
    }

    [Test]
    public async Task IdleDeadlineRollsBackAndReleasesWaitingAcquisition()
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await database.Engine.BeginTransactionAsync(new(TimeSpan.FromMilliseconds(200)));
        await using var next = await database.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(5))).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        transaction.State.Should().Be(StorageTransactionState.Aborted);
        await next.RollbackAsync();
    }

    [Test]
    public async Task ConcurrentOperationRejectsAndCancelledWaitDoesNotReleaseOwner()
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await database.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var statement = transaction.ExecuteStatementAsync(async (_, _) => { entered.SetResult(); await release.Task; }).AsTask();
        await entered.Task;
        Func<Task> commit = async () => await transaction.CommitAsync();
        await commit.Should().ThrowAsync<InvalidOperationException>();
        using var cancel = new CancellationTokenSource();
        var waiting = database.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)), cancel.Token).AsTask();
        cancel.Cancel();
        Func<Task> acquire = async () => await waiting;
        await acquire.Should().ThrowAsync<OperationCanceledException>();
        release.SetResult(); await statement;
        transaction.State.Should().Be(StorageTransactionState.Committable);
        await transaction.RollbackAsync();
    }
}
