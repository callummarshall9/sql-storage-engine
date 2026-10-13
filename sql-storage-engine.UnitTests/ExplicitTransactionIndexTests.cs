// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models;
using sql_storage_engine.Exposures.Transactions;
using sql_storage_engine.Exposures.Rows;

namespace sql_storage_engine.UnitTests;
public sealed class ExplicitTransactionIndexTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task InsertDeleteAndKeyChangesResolveWithTheirIndexEntries(bool commit)
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await database.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(10)));
        await transaction.ExecuteStatementAsync(async (context, token) =>
        {
            var table = await context.OpenTableAsync(database.Table.Id, token);
            await table.UpdateAsync(database.First, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(3) }]), token);
            await table.DeleteAsync(database.Second, token);
            await table.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(4), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(100)]), token);
            var index = await context.OpenIndexAsync(database.Index, token);
            (await index.FindAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)], token)).Should().BeEmpty();
            (await index.FindAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2)], token)).Should().BeEmpty();
            (await index.FindAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(3)], token)).Should().ContainSingle();
            (await index.FindAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(4)], token)).Should().ContainSingle();
        });
        if (commit)
            await transaction.CommitAsync();
        else
            await transaction.RollbackAsync();
        await database.ReopenAsync();
        var index = await database.Engine.OpenIndexAsync(database.Index);
        foreach (var value in new[]
        {
            1,
            2,
            3,
            4
        }

        )
            (await index.FindAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(value)])).Count.Should().Be((value >= 3) == commit ? 1 : 0);
    }
}