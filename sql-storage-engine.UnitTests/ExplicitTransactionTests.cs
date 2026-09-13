using AwesomeAssertions;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;

public sealed class ExplicitTransactionTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task MultipleCallbacksPublishOrRollbackTogetherAndReceiptSurvivesReopen(bool commit)
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await database.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        await transaction.ExecuteStatementAsync(async (context, token) =>
        {
            var table = await context.OpenTableAsync(database.Table.Id, token);
            await table.UpdateAsync(database.First, new RowUpdate([new ColumnUpdate(1, SqlValue.Integer(90))]), token);
        });
        await transaction.ExecuteStatementAsync(async (context, token) =>
        {
            var table = await context.OpenTableAsync(database.Table.Id, token);
            ((IntegerSqlValue)(await table.GetAsync(database.First, token))!.Row.Values[1]).Value.Should().Be(90);
            await table.UpdateAsync(database.Second, new RowUpdate([new ColumnUpdate(1, SqlValue.Integer(110))]), token);
            var index = await context.OpenIndexAsync(database.Index, token);
            (await index.FindAsync([SqlValue.Integer(2)], token)).Should().Equal(database.Second);
            context.Catalog.Tables.Should().ContainSingle();
        });
        var receipt = commit ? await transaction.CommitAsync() : await transaction.RollbackAsync();
        receipt.State.Should().Be(commit ? StorageTransactionState.Committed : StorageTransactionState.Aborted);
        (await database.BalancesAsync()).Should().Equal(commit ? [90L, 110L] : [100L, 100L]);
        var repeated = commit ? await transaction.CommitAsync() : await transaction.RollbackAsync();
        repeated.Should().Be(receipt);
        await database.ReopenAsync();
        (await database.Engine.ResolveTransactionAsync(receipt.Identity)).Should().Be(receipt);
        (await database.BalancesAsync()).Should().Equal(commit ? [90L, 110L] : [100L, 100L]);
    }

    [Test]
    public async Task FailedStatementRestoresOnlyItsCallbackAndRootRemainsCommittable()
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await database.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        await transaction.ExecuteStatementAsync(async (context, token) =>
            await (await context.OpenTableAsync(database.Table.Id, token)).UpdateAsync(database.First, new RowUpdate([new ColumnUpdate(1, SqlValue.Integer(80))]), token));
        Func<Task> fail = async () => await transaction.ExecuteStatementAsync(async (context, token) =>
        {
            await (await context.OpenTableAsync(database.Table.Id, token)).UpdateAsync(database.Second, new RowUpdate([new ColumnUpdate(1, SqlValue.Integer(999))]), token);
            throw new InvalidOperationException("callback failed");
        });
        await fail.Should().ThrowAsync<InvalidOperationException>();
        transaction.State.Should().Be(StorageTransactionState.Committable);
        await transaction.CommitAsync();
        (await database.BalancesAsync()).Should().Equal(80, 100);
    }

    [Test]
    public async Task CancelledCallbackAbortsEarlierWorkAndDisposalIsIdempotent()
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        var transaction = await database.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        await transaction.ExecuteStatementAsync(async (context, token) =>
            await (await context.OpenTableAsync(database.Table.Id, token)).UpdateAsync(database.First, new RowUpdate([new ColumnUpdate(1, SqlValue.Integer(70))]), token));
        using var cancel = new CancellationTokenSource();
        Func<Task> execute = async () => await transaction.ExecuteStatementAsync((_, token) =>
        {
            cancel.Cancel(); token.ThrowIfCancellationRequested(); return ValueTask.CompletedTask;
        }, cancel.Token);
        await execute.Should().ThrowAsync<OperationCanceledException>();
        transaction.State.Should().Be(StorageTransactionState.Aborted);
        (await database.BalancesAsync()).Should().Equal(100, 100);
        await transaction.DisposeAsync(); await transaction.DisposeAsync();
    }

    [Test]
    public async Task ScopedHandlesCannotEscapeAndCatalogCannotSeeUncommittedDdl()
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await database.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        IStorageTable? escaped = null;
        IStorageCatalog? catalog = null;
        IStorageIndex? index = null;
        await transaction.ExecuteStatementAsync(async (context, token) =>
        {
            escaped = await context.OpenTableAsync(database.Table.Id, token);
            catalog = context.Catalog;
            index = await context.OpenIndexAsync(database.Index, token);
        });
        Action readCatalog = () => _ = catalog!.Tables;
        readCatalog.Should().Throw<InvalidOperationException>();
        Func<Task> read = async () => await escaped!.GetAsync(database.First);
        await read.Should().ThrowAsync<InvalidOperationException>();
        Func<Task> find = async () => await index!.FindAsync([SqlValue.Integer(1)]);
        await find.Should().ThrowAsync<InvalidOperationException>();
        Action otherCatalog = () => _ = database.Engine.Catalog.Tables;
        otherCatalog.Should().Throw<InvalidOperationException>();
        await transaction.RollbackAsync();
    }
}
