using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

namespace sql_storage_engine.UnitTests;
internal sealed class ExplicitTransactionTestDatabase : IAsyncDisposable
{
    private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "explicit-transaction-" + Guid.NewGuid().ToString("N"));
    internal string Path => System.IO.Path.Combine(_directory,"database.db");
    internal sql_storage_engine.Exposures.Storage.StorageSessionExposure Engine { get; private set; } = null!;
    internal CatalogTable Table { get; private set; } = null!;
    internal IndexId Index { get; private set; }
    internal RowId First { get; private set; }
    internal RowId Second { get; private set; }

    internal static async Task<ExplicitTransactionTestDatabase> CreateAsync()
    {
        var result = new ExplicitTransactionTestDatabase();
        Directory.CreateDirectory(result._directory);
        result.Engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(result.Path);
        result.Table = await result.Engine.CreateCatalogTableAsync("balances",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "balance", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)]);
        result.Index = (await result.Engine.CreateCatalogIndexAsync("by_id",result.Table.Id,true,[sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.Last)])).Id;
        var table = await result.Engine.OpenTableAsync(result.Table.Id);
        result.First = await table.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(100)]));
        result.Second = await table.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(100)]));
        return result;
    }

    internal async Task ReopenAsync()
    {
        await Engine.DisposeAsync();
        Engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(Path);
    }

    internal async Task<long[]> BalancesAsync()
    {
        var table = await Engine.OpenTableAsync(Table.Id);
        return[((IntegerSqlValue)(await table.GetAsync(First))!.Row.Values[1]).Value, ((IntegerSqlValue)(await table.GetAsync(Second))!.Row.Values[1]).Value];
    }

    public async ValueTask DisposeAsync()
    {
        await Engine.DisposeAsync();
        Directory.Delete(_directory, true);
    }
}