using AwesomeAssertions;
using sql_storage_engine.Rows;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;

public sealed class RepeatableReadTests
{
    [Test]
    public async Task RepeatableRead_PreventsChangeBetweenReadsAndWriterWaitsUntilCompletion()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync();
        var rowId = await fixture.Table.InsertAsync(new Row([SqlValue.Integer(7), SqlValue.Text("old")]));
        var manager = new LockManager();
        var transactions = new TransactionManager();
        var readerTransaction = new LockingTransaction(transactions.Begin(), manager);
        var writerTransaction = new LockingTransaction(transactions.Begin(), manager);
        var reader = new TransactionalTable(fixture.Table, readerTransaction, TransactionIsolationLevel.RepeatableRead);
        var writer = new TransactionalTable(fixture.Table, writerTransaction);

        var firstRead = await reader.TryGetAsync(rowId);
        var update = writer.UpdateAsync(rowId,
            new RowUpdate([new ColumnUpdate(1, SqlValue.Text("new"))])).AsTask();

        update.IsCompleted.Should().BeFalse();
        (await reader.TryGetAsync(rowId)).Row!.Values.Should().BeEquivalentTo(firstRead.Row!.Values);
        readerTransaction.Commit();
        await update;
        writerTransaction.Commit();
        (await fixture.Table.TryGetAsync(rowId)).Row!.Values[1].Should().Be(SqlValue.Text("new"));
    }

    [Test]
    public async Task ReadCommitted_ReleasesReadLockAndAllowsNonRepeatableRead()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync();
        var rowId = await fixture.Table.InsertAsync(new Row([SqlValue.Integer(7), SqlValue.Text("old")]));
        var manager = new LockManager();
        var transactions = new TransactionManager();
        var readerTransaction = new LockingTransaction(transactions.Begin(), manager);
        var writerTransaction = new LockingTransaction(transactions.Begin(), manager);
        var reader = new TransactionalTable(fixture.Table, readerTransaction, TransactionIsolationLevel.ReadCommitted);
        var writer = new TransactionalTable(fixture.Table, writerTransaction);

        (await reader.TryGetAsync(rowId)).Row!.Values[1].Should().Be(SqlValue.Text("old"));
        await writer.UpdateAsync(rowId, new RowUpdate([new ColumnUpdate(1, SqlValue.Text("new"))]));
        writerTransaction.Commit();

        (await reader.TryGetAsync(rowId)).Row!.Values[1].Should().Be(SqlValue.Text("new"));
        readerTransaction.Commit();
    }

    [Test]
    public async Task ExclusiveRowLocks_PreventLostPartialUpdates()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync();
        var rowId = await fixture.Table.InsertAsync(new Row([SqlValue.Integer(7), SqlValue.Text("old")]));
        var manager = new LockManager();
        var transactions = new TransactionManager();
        var firstTransaction = new LockingTransaction(transactions.Begin(), manager);
        var secondTransaction = new LockingTransaction(transactions.Begin(), manager);
        var first = new TransactionalTable(fixture.Table, firstTransaction, TransactionIsolationLevel.RepeatableRead);
        var second = new TransactionalTable(fixture.Table, secondTransaction, TransactionIsolationLevel.RepeatableRead);

        await first.UpdateAsync(rowId, new RowUpdate([new ColumnUpdate(0, SqlValue.Integer(8))]));
        var secondUpdate = second.UpdateAsync(rowId,
            new RowUpdate([new ColumnUpdate(1, SqlValue.Text("new"))])).AsTask();
        secondUpdate.IsCompleted.Should().BeFalse();
        firstTransaction.Commit();
        await secondUpdate;
        secondTransaction.Commit();

        (await fixture.Table.TryGetAsync(rowId)).Row!.Values.Should().BeEquivalentTo(
            [SqlValue.Integer(8), SqlValue.Text("new")]);
    }
}
