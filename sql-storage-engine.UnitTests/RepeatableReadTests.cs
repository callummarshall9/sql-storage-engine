// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Rows;
using sql_storage_engine.Transactions;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;

using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.UnitTests;
public sealed class RepeatableReadTests
{
    [Test]
    public async Task RepeatableRead_PreventsChangeBetweenReadsAndWriterWaitsUntilCompletion()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync();
        var rowId = await fixture.Table.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("old")]));
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        sql_storage_engine.Exposures.Transactions.TransactionManagerExposure transactions = new sql_storage_engine.Exposures.Transactions.TransactionManagerExposure();
        var readerTransaction = new sql_storage_engine.Exposures.Transactions.LockingTransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionExposure(transactions.CreateTransactionLifecycle()), manager);
        var writerTransaction = new sql_storage_engine.Exposures.Transactions.LockingTransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionExposure(transactions.CreateTransactionLifecycle()), manager);
        var reader = new sql_storage_engine.Exposures.Transactions.TransactionalTableExposure(fixture.Table, readerTransaction, new TransactionalTableOptions { IsolationLevel = TransactionIsolationLevel.RepeatableRead });
        var writer = new sql_storage_engine.Exposures.Transactions.TransactionalTableExposure(fixture.Table, writerTransaction);
        var firstRead = await reader.TryGetAsync(rowId);
        var update = writer.UpdateAsync(rowId, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("new") }])).AsTask();
        update.IsCompleted.Should().BeFalse();
        (await reader.TryGetAsync(rowId)).Row!.Values.Should().BeEquivalentTo(firstRead.Row!.Values);
        readerTransaction.Commit();
        await update;
        writerTransaction.Commit();
        (await fixture.Table.TryGetAsync(rowId)).Row!.Values[1].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("new"));
    }

    [Test]
    public async Task ReadCommitted_ReleasesReadLockAndAllowsNonRepeatableRead()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync();
        var rowId = await fixture.Table.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("old")]));
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        sql_storage_engine.Exposures.Transactions.TransactionManagerExposure transactions = new sql_storage_engine.Exposures.Transactions.TransactionManagerExposure();
        var readerTransaction = new sql_storage_engine.Exposures.Transactions.LockingTransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionExposure(transactions.CreateTransactionLifecycle()), manager);
        var writerTransaction = new sql_storage_engine.Exposures.Transactions.LockingTransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionExposure(transactions.CreateTransactionLifecycle()), manager);
        var reader = new sql_storage_engine.Exposures.Transactions.TransactionalTableExposure(fixture.Table, readerTransaction, new TransactionalTableOptions { IsolationLevel = TransactionIsolationLevel.ReadCommitted });
        var writer = new sql_storage_engine.Exposures.Transactions.TransactionalTableExposure(fixture.Table, writerTransaction);
        (await reader.TryGetAsync(rowId)).Row!.Values[1].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("old"));
        await writer.UpdateAsync(rowId, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("new") }]));
        writerTransaction.Commit();
        (await reader.TryGetAsync(rowId)).Row!.Values[1].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("new"));
        readerTransaction.Commit();
    }

    [Test]
    public async Task ExclusiveRowLocks_PreventLostPartialUpdates()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync();
        var rowId = await fixture.Table.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("old")]));
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        sql_storage_engine.Exposures.Transactions.TransactionManagerExposure transactions = new sql_storage_engine.Exposures.Transactions.TransactionManagerExposure();
        var firstTransaction = new sql_storage_engine.Exposures.Transactions.LockingTransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionExposure(transactions.CreateTransactionLifecycle()), manager);
        var secondTransaction = new sql_storage_engine.Exposures.Transactions.LockingTransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionExposure(transactions.CreateTransactionLifecycle()), manager);
        var first = new sql_storage_engine.Exposures.Transactions.TransactionalTableExposure(fixture.Table, firstTransaction, new TransactionalTableOptions { IsolationLevel = TransactionIsolationLevel.RepeatableRead });
        var second = new sql_storage_engine.Exposures.Transactions.TransactionalTableExposure(fixture.Table, secondTransaction, new TransactionalTableOptions { IsolationLevel = TransactionIsolationLevel.RepeatableRead });
        await first.UpdateAsync(rowId, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(8) }]));
        var secondUpdate = second.UpdateAsync(rowId, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("new") }])).AsTask();
        secondUpdate.IsCompleted.Should().BeFalse();
        firstTransaction.Commit();
        await secondUpdate;
        secondTransaction.Commit();
        (await fixture.Table.TryGetAsync(rowId)).Row!.Values.Should().BeEquivalentTo([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(8), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("new")]);
    }
}