using AwesomeAssertions;
using sql_storage_engine.Models;
using sql_storage_engine.Exposures.Transactions;

namespace sql_storage_engine.UnitTests;
public sealed class SavepointRecoveryTests
{
    [TestCase("BeforeSavepointCreate")]
    [TestCase("AfterSavepointCreate")]
    [TestCase("BeforeSavepointRestore")]
    public async Task CancellationBeforePublicationOrRewriteKeepsRootCommittable(string stage)
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        var point = await transaction.CreateStorageSavepointAsync("before");
        await SavepointTests.SetBalance(transaction, db, 75);
        using var cancellation = new CancellationTokenSource();
        db.Engine.WriteTransactionObserver(observer: current =>
        {
            if (current == stage)
                cancellation.Cancel();
        });
        Func<Task> action = async () =>
        {
            if (stage.Contains("Create", StringComparison.Ordinal))
                await transaction.CreateStorageSavepointAsync("cancelled", cancellation.Token);
            else
                await transaction.RollbackToSavepointAsync(point, cancellation.Token);
        };
        await action.Should().ThrowAsync<OperationCanceledException>();
        transaction.ReadState().Should().Be(StorageTransactionState.Committable);
        db.Engine.WriteTransactionObserver(observer: null);
        await SavepointTests.AssertBalance(transaction, db, 75);
        await transaction.RollbackToSavepointAsync(point);
        await SavepointTests.AssertBalance(transaction, db, 100);
        (await transaction.CommitAsync()).State.Should().Be(StorageTransactionState.Committed);
    }

    [Test]
    public async Task CancellationAfterRestoreFinishesTheOperationWithoutAbortingRoot()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        var point = await transaction.CreateStorageSavepointAsync("before");
        await SavepointTests.SetBalance(transaction, db, 75);
        using var cancellation = new CancellationTokenSource();
        db.Engine.WriteTransactionObserver(observer: stage =>
        {
            if (stage == "AfterSavepointRestore")
                cancellation.Cancel();
        });
        await transaction.RollbackToSavepointAsync(point, cancellation.Token);
        cancellation.IsCancellationRequested.Should().BeTrue();
        transaction.ReadState().Should().Be(StorageTransactionState.Committable);
        db.Engine.WriteTransactionObserver(observer: null);
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
        await using var transaction = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        var point = await transaction.CreateStorageSavepointAsync("before");
        await SavepointTests.SetBalance(transaction, db, 75);
        db.Engine.WriteTransactionObserver(observer: stage =>
        {
            if (stage == "AfterSavepointRestore" || recoveryFails && stage == "BeforeSavepointRecovery")
                throw new IOException("injected");
        });
        Func<Task> restore = async () => await transaction.RollbackToSavepointAsync(point);
        if (recoveryFails)
        {
            await restore.Should().ThrowAsync<AggregateException>();
            transaction.ReadState().Should().Be(StorageTransactionState.Doomed);
            Func<Task> forbidden = async () => await transaction.CreateStorageSavepointAsync("forbidden");
            await forbidden.Should().ThrowAsync<InvalidOperationException>();
        }
        else
        {
            await restore.Should().ThrowAsync<IOException>();
            transaction.ReadState().Should().Be(StorageTransactionState.Committable);
            await SavepointTests.AssertBalance(transaction, db, 75);
        }

        db.Engine.WriteTransactionObserver(observer: null);
        if (recoveryFails)
            await transaction.RollbackAsync();
        else
        {
            await transaction.RollbackToSavepointAsync(point);
            await transaction.CommitAsync();
        }

        await db.ReopenAsync();
        (await db.BalancesAsync()).Should().Equal(100, 100);
    }
}