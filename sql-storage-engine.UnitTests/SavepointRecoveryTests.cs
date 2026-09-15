using AwesomeAssertions;

namespace sql_storage_engine.UnitTests;

public sealed class SavepointRecoveryTests
{
    [TestCase("BeforeSavepointCreate")]
    [TestCase("AfterSavepointCreate")]
    [TestCase("BeforeSavepointRestore")]
    public async Task CancellationBeforePublicationOrRewriteKeepsRootCommittable(string stage)
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        var point = await transaction.CreateSavepointAsync("before");
        await SavepointTests.SetBalance(transaction, db, 75);
        using var cancellation = new CancellationTokenSource();
        db.Engine.TransactionObserver = current => { if (current == stage) cancellation.Cancel(); };
        Func<Task> action = async () =>
        {
            if (stage.Contains("Create", StringComparison.Ordinal)) await transaction.CreateSavepointAsync("cancelled", cancellation.Token);
            else await transaction.RollbackToSavepointAsync(point, cancellation.Token);
        };
        await action.Should().ThrowAsync<OperationCanceledException>();
        transaction.State.Should().Be(StorageTransactionState.Committable);
        db.Engine.TransactionObserver = null;
        await SavepointTests.AssertBalance(transaction, db, 75);
        await transaction.RollbackToSavepointAsync(point);
        await SavepointTests.AssertBalance(transaction, db, 100);
        (await transaction.CommitAsync()).State.Should().Be(StorageTransactionState.Committed);
    }

    [Test]
    public async Task CancellationAfterRestoreFinishesTheOperationWithoutAbortingRoot()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        var point = await transaction.CreateSavepointAsync("before");
        await SavepointTests.SetBalance(transaction, db, 75);
        using var cancellation = new CancellationTokenSource();
        db.Engine.TransactionObserver = stage => { if (stage == "AfterSavepointRestore") cancellation.Cancel(); };
        await transaction.RollbackToSavepointAsync(point, cancellation.Token);
        cancellation.IsCancellationRequested.Should().BeTrue();
        transaction.State.Should().Be(StorageTransactionState.Committable);
        db.Engine.TransactionObserver = null;
        await SavepointTests.SetBalance(transaction, db, 60);
        await transaction.CommitAsync();
        await db.ReopenAsync();
        (await db.BalancesAsync()).Should().Equal(60, 100);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task FailedRewriteRecoversPreviousStateOrDoomsRootIfRecoveryFails(bool recoveryFails)
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        var point = await transaction.CreateSavepointAsync("before");
        await SavepointTests.SetBalance(transaction, db, 75);
        db.Engine.TransactionObserver = stage =>
        {
            if (stage == "AfterSavepointRestore" || recoveryFails && stage == "BeforeSavepointRecovery") throw new IOException("injected");
        };
        Func<Task> restore = async () => await transaction.RollbackToSavepointAsync(point);
        if (recoveryFails)
        {
            await restore.Should().ThrowAsync<AggregateException>();
            transaction.State.Should().Be(StorageTransactionState.Doomed);
            Func<Task> forbidden = async () => await transaction.CreateSavepointAsync("forbidden");
            await forbidden.Should().ThrowAsync<InvalidOperationException>();
        }
        else
        {
            await restore.Should().ThrowAsync<IOException>();
            transaction.State.Should().Be(StorageTransactionState.Committable);
            await SavepointTests.AssertBalance(transaction, db, 75);
        }
        db.Engine.TransactionObserver = null;
        if (recoveryFails) await transaction.RollbackAsync();
        else { await transaction.RollbackToSavepointAsync(point); await transaction.CommitAsync(); }
        await db.ReopenAsync();
        (await db.BalancesAsync()).Should().Equal(100, 100);
    }
}
