// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Diagnostics;
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models;
using sql_storage_engine.Exposures.Transactions;
using sql_storage_engine.Exposures.Rows;

namespace sql_storage_engine.UnitTests;
public sealed class ExplicitTransactionResourceTests
{
    [TestCase(1)]
    [TestCase(128)]
    public async Task RetainsMeasuredSnapshotCostAndReadsItsOwnWrites(int callbacks)
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        var timer = Stopwatch.StartNew();
        await using var transaction = await database.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        var bytes = new FileInfo(database.Path + ".transaction-undo").Length;
        for (var i = 0; i < callbacks; i++)
        {
            var expected = i;
            await transaction.ExecuteStatementAsync(async (context, token) =>
            {
                var table = await context.OpenTableAsync(database.Table.Id, token);
                await table.UpdateAsync(database.First, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(expected) }]), token);
                ((IntegerSqlValue)(await table.GetAsync(database.First, token))!.Row.Values[1]).Value.Should().Be(expected);
            });
        }

        (await transaction.CommitAsync()).State.Should().Be(StorageTransactionState.Committed);
        TestContext.Out.WriteLine($"transaction callbacks={callbacks}; initialJournalBytes={bytes}; elapsedMilliseconds={timer.Elapsed.TotalMilliseconds:F2}; no timing threshold");
    }
}