// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Rows;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Transactions;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.UnitTests;
public sealed class SavepointTests
{
    [Test]
    public async Task RestoresHeapIndexCatalogAndRetainsEarlierWorkForCommit()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        await SetBalance(transaction, db, 75);
        var point = await transaction.CreateStorageSavepointAsync("transfer");
        var length = new FileInfo(db.Path).Length;
        await transaction.ExecuteStatementAsync(async (context, token) =>
        {
            var table = await context.OpenTableAsync(db.Table.Id, token);
            await table.UpdateAsync(db.First, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(9) }, new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(25) }]), token);
            await table.DeleteAsync(db.Second, token);
            await table.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(3), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(500)]), token);
            await context.CreateCatalogTableAsync(sql_storage_engine.Exposures.Catalog.CatalogTableNameExposure.CreateCatalogTableName("master","dbo","later"), [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)], cancellationToken: token);
        });
        await transaction.RollbackToSavepointAsync(point);
        transaction.ReadState().Should().Be(StorageTransactionState.Committable);
        new FileInfo(db.Path).Length.Should().Be(length);
        await transaction.ExecuteStatementAsync(async (context, token) =>
        {
            context.ReadCatalog().ReadTables().Any(table => table.Name == "later").Should().BeFalse();
            var index = await context.OpenIndexAsync(db.Index, token);
            (await index.FindAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(9)], token)).Should().BeEmpty();
            (await index.FindAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)], token)).Should().ContainSingle();
            var table = await context.OpenTableAsync(db.Table.Id, token);
            ((IntegerSqlValue)(await table.GetAsync(db.First, token))!.Row.Values[1]).Value.Should().Be(75);
            ((IntegerSqlValue)(await table.GetAsync(db.Second, token))!.Row.Values[1]).Value.Should().Be(100);
        });
        await SetBalance(transaction, db, 60);
        await transaction.RollbackToSavepointAsync(point);
        (await transaction.CommitAsync()).State.Should().Be(StorageTransactionState.Committed);
        await db.ReopenAsync();
        (await db.BalancesAsync()).Should().Equal(75, 100);
        Directory.GetFiles(Path.GetDirectoryName(db.Path)!, "*.savepoint-*").Should().BeEmpty();
    }

    [Test]
    public async Task DuplicateNamesSelectNewestAndRollbackInvalidatesOnlyLaterPositions()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        var first = await transaction.CreateStorageSavepointAsync("same");
        await SetBalance(transaction, db, 80);
        var middle = await transaction.CreateStorageSavepointAsync("middle");
        await SetBalance(transaction, db, 60);
        var newest = await transaction.CreateStorageSavepointAsync("same");
        await SetBalance(transaction, db, 40);
        await transaction.RollbackToSavepointAsync("same");
        await AssertBalance(transaction, db, 60);
        await transaction.RollbackToSavepointAsync(middle);
        await AssertBalance(transaction, db, 80);
        Func<Task> invalid = async () => await transaction.RollbackToSavepointAsync(newest);
        await invalid.Should().ThrowAsync<ArgumentException>();
        await transaction.RollbackToSavepointAsync("same");
        await AssertBalance(transaction, db, 100);
        await transaction.RollbackToSavepointAsync(first);
        (await transaction.CommitAsync()).State.Should().Be(StorageTransactionState.Committed);
    }

    [Test]
    public async Task InvalidNamesTokensAndStatesHaveNoEffects()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var transaction = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        foreach (var name in new[]
        {
            "",
            new string ('x', 33),
            null!
        }

        )
        {
            Func<Task> invalid = async () => await transaction.CreateStorageSavepointAsync(name);
            await invalid.Should().ThrowAsync<ArgumentException>();
        }

        var point = await transaction.CreateStorageSavepointAsync(new string ('x', 32));
        foreach (var foreign in new[]
        {
            point with
            {
                Value = Guid.NewGuid()
            },
            point with
            {
                Transaction = new sql_storage_engine.Models.StorageTransactionIdentity
                {
                    DatabaseId = db.Engine.ReadDatabaseId(),
                    Value = Guid.NewGuid()
                }
            },
            point with
            {
                Name = "changed"
            }
        }

        )
        {
            Func<Task> invalid = async () => await transaction.RollbackToSavepointAsync(foreign);
            await invalid.Should().ThrowAsync<ArgumentException>();
        }

        Func<Task> missing = async () => await transaction.RollbackToSavepointAsync("UNKNOWN");
        await missing.Should().ThrowAsync<ArgumentException>();
        await transaction.RollbackAsync();
        Func<Task> terminal = async () => await transaction.CreateStorageSavepointAsync("later");
        await terminal.Should().ThrowAsync<InvalidOperationException>();
        await transaction.DisposeAsync();
        Func<Task> disposed = async () => await transaction.RollbackToSavepointAsync(point);
        await disposed.Should().ThrowAsync<ObjectDisposedException>();
        (await db.BalancesAsync()).Should().Equal(100, 100);
    }

    internal static ValueTask SetBalance(IStorageTransaction transaction, ExplicitTransactionTestDatabase db, long value) => transaction.ExecuteStatementAsync(async (context, token) => await (await context.OpenTableAsync(db.Table.Id,token)).UpdateAsync(db.First,RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(value) }]),token));
    internal static ValueTask AssertBalance(IStorageTransaction transaction, ExplicitTransactionTestDatabase db, long expected) => transaction.ExecuteStatementAsync(async (context, token) => ((IntegerSqlValue)(await (await context.OpenTableAsync(db.Table.Id,token)).GetAsync(db.First,token))!.Row.Values[1]).Value.Should().Be(expected));
}