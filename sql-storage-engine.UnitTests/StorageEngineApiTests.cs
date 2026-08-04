using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;

public sealed class StorageEngineApiTests
{
    [Test]
    public async Task PublicApi_SupportsCatalogBindingAndLogicalRowAccessAcrossReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"storage-api-{Guid.NewGuid():N}.db");
        try
        {
            await using (var engine = await StorageEngine.CreateAsync(path,
                new StorageEngineOptions { BufferPoolCapacity = 16, InlineValueThreshold = 8 }))
            {
                var table = await engine.CreateTableAsync("items",
                [
                    new CatalogColumn(new ColumnId(1), "id", SqlType.Integer, false),
                    new CatalogColumn(new ColumnId(2), "name", SqlType.Text, true)
                ]);
                var index = await engine.CreateIndexAsync("items_by_id", table.Id, true,
                [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.Last)]);

                engine.Catalog.TryGetTable("items", out var bound).Should().BeTrue();
                bound!.Columns.Select(column => column.Name).Should().Equal("id", "name");

                var rows = await engine.OpenTableAsync(table.Id);
                var rowId = await rows.InsertAsync(new Row([SqlValue.Integer(7), SqlValue.Text("a long value")]));
                (await rows.GetAsync(rowId))!.Row.Values.Should().Equal(SqlValue.Integer(7), SqlValue.Text("a long value"));

                var scanned = new List<StoredRow>();
                await foreach (var row in rows.ScanAsync()) scanned.Add(row);
                scanned.Select(row => row.RowId).Should().Equal(rowId);

                var lookup = await engine.OpenIndexAsync(index.Id);
                (await lookup.FindAsync([SqlValue.Integer(7)])).Should().Equal(rowId);
            }

            await using (var reopened = await StorageEngine.OpenAsync(path,
                new StorageEngineOptions { BufferPoolCapacity = 16, InlineValueThreshold = 8 }))
            {
                reopened.Catalog.TryGetTable("items", out var table).Should().BeTrue();
                var rows = await reopened.OpenTableAsync(table!.Id);
                var scanned = new List<StoredRow>();
                await foreach (var row in rows.ScanAsync()) scanned.Add(row);
                scanned.Should().ContainSingle();
                ((IntegerSqlValue)scanned[0].Row.Values[0]).Value.Should().Be(7);
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
