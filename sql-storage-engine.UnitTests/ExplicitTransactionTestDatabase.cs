using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;

internal sealed class ExplicitTransactionTestDatabase : IAsyncDisposable
{
    private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "explicit-transaction-" + Guid.NewGuid().ToString("N"));
    internal string Path => System.IO.Path.Combine(_directory, "database.db");
    internal StorageEngine Engine { get; private set; } = null!;
    internal CatalogTable Table { get; private set; } = null!;
    internal IndexId Index { get; private set; }
    internal RowId First { get; private set; }
    internal RowId Second { get; private set; }

    internal static async Task<ExplicitTransactionTestDatabase> CreateAsync()
    {
        var result = new ExplicitTransactionTestDatabase();
        Directory.CreateDirectory(result._directory);
        result.Engine = await StorageEngine.CreateAsync(result.Path);
        result.Table = await result.Engine.CreateTableAsync("balances", [new(new ColumnId(1), "id", SqlType.Int, false), new(new ColumnId(2), "balance", SqlType.Int, false)]);
        result.Index = (await result.Engine.CreateIndexAsync("by_id", result.Table.Id, true,
            [new(new ColumnId(1), SortDirection.Ascending, NullSortOrder.Last)])).Id;
        var table = await result.Engine.OpenTableAsync(result.Table.Id);
        result.First = await table.InsertAsync(new Row([SqlValue.Integer(1), SqlValue.Integer(100)]));
        result.Second = await table.InsertAsync(new Row([SqlValue.Integer(2), SqlValue.Integer(100)]));
        return result;
    }

    internal async Task ReopenAsync()
    {
        await Engine.DisposeAsync();
        Engine = await StorageEngine.OpenAsync(Path);
    }

    internal async Task<long[]> BalancesAsync()
    {
        var table = await Engine.OpenTableAsync(Table.Id);
        return [((IntegerSqlValue)(await table.GetAsync(First))!.Row.Values[1]).Value,
            ((IntegerSqlValue)(await table.GetAsync(Second))!.Row.Values[1]).Value];
    }

    public async ValueTask DisposeAsync()
    {
        await Engine.DisposeAsync();
        Directory.Delete(_directory, true);
    }
}
