using AwesomeAssertions;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;

public sealed class ExplicitTransactionRecoveryTests
{
    [TestCase("BeforeCommitDecision", StorageTransactionState.Aborted)]
    [TestCase("AfterCommitDecision", StorageTransactionState.Committed)]
    [TestCase("AfterReceipt", StorageTransactionState.Committed)]
    public async Task DecisionFaultResolvesFromDurableEvidence(string stage, StorageTransactionState expected)
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        var transaction = await database.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        await transaction.ExecuteStatementAsync(async (context, token) =>
            await (await context.OpenTableAsync(database.Table.Id, token)).UpdateAsync(database.First,
                new RowUpdate([new ColumnUpdate(1, SqlValue.Integer(75))]), token));
        database.Engine.TransactionObserver = current => { if (current == stage) throw new IOException("injected"); };
        var result = await transaction.CommitAsync();
        result.State.Should().Be(stage == "BeforeCommitDecision" ? StorageTransactionState.Aborted : StorageTransactionState.Indeterminate);
        if (result.State == StorageTransactionState.Indeterminate)
        {
            Func<Task> read = async () => await database.BalancesAsync();
            await read.Should().ThrowAsync<InvalidOperationException>();
            Func<Task> dispose = async () => await transaction.DisposeAsync();
            await dispose.Should().ThrowAsync<InvalidOperationException>();
        }
        else await transaction.DisposeAsync();
        await database.ReopenAsync();
        (await database.Engine.ResolveTransactionAsync(result.Identity)).State.Should().Be(expected);
        (await database.BalancesAsync()).Should().Equal(expected == StorageTransactionState.Committed ? [75L, 100L] : [100L, 100L]);
    }

    [Test]
    public async Task FailedRollbackQuarantinesUntilRecoveryAndReceiptCanBeReleased()
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        var transaction = await database.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        await transaction.ExecuteStatementAsync(async (context, token) =>
            await (await context.OpenTableAsync(database.Table.Id, token)).UpdateAsync(database.First,
                new RowUpdate([new ColumnUpdate(1, SqlValue.Integer(75))]), token));
        database.Engine.TransactionObserver = stage => { if (stage == "BeforeRollback") throw new IOException("injected"); };
        var result = await transaction.RollbackAsync();
        result.State.Should().Be(StorageTransactionState.Indeterminate);
        Func<Task> dispose = async () => await transaction.DisposeAsync();
        await dispose.Should().ThrowAsync<InvalidOperationException>();
        await database.ReopenAsync();
        (await database.Engine.ResolveTransactionAsync(result.Identity)).State.Should().Be(StorageTransactionState.Aborted);
        (await database.BalancesAsync()).Should().Equal(100, 100);
        await database.Engine.ReleaseTransactionReceiptAsync(result.Identity);
        (await database.Engine.ResolveTransactionAsync(result.Identity)).State.Should().Be(StorageTransactionState.Indeterminate);
    }
}
