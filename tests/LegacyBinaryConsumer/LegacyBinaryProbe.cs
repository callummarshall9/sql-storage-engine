using sql_storage_engine;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;

public static class LegacyBinaryProbe
{
    public static async Task Main(string[] args)
    {
        await using var engine = await StorageEngine.CreateAsync(args[0]);
        var table = await engine.CreateTableAsync(new CatalogTableName("master", "dbo", "binary"),
            [new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false)]);
        await (await engine.OpenTableAsync(table.Id)).InsertAsync(new Row([SqlValue.Integer(73)]));
    }

    public static async Task ValidateAsync(string path)
    {
        // This IL references physical types in sql-storage-engine, before extraction.
        var page = new PageId(17);
        if (page.Value != 17 || new sql_storage_engine.Indexes.IndexKey([1]).CompareTo(new([2])) >= 0)
            throw new Exception("Forwarded physical API changed.");
        await using var engine = await StorageEngine.OpenAsync(path);
        var table = engine.Catalog.Tables.Single(t => t.Name == "binary");
        var count = 0;
        await foreach (var row in (await engine.OpenTableAsync(table.Id)).ScanAsync())
        {
            if (!row.Row.Values[0].Equals(SqlValue.Integer(73))) throw new Exception("Baseline file value changed.");
            count++;
        }
        if (count != 1) throw new Exception("Baseline file row count changed.");
    }
}
