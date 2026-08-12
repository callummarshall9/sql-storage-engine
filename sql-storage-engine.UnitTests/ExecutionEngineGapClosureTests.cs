using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Rows;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;

public sealed class ExecutionEngineGapClosureTests
{
    [Test]
    public async Task QualifiedTables_AreUnambiguousAndSurviveReopen()
    {
        var path = TemporaryPath();
        try
        {
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                await engine.CreateTableAsync(new CatalogTableName("app", "sales", "orders"), Columns());
                await engine.CreateTableAsync(new CatalogTableName("app", "archive", "orders"), Columns());
                engine.Catalog.TryGetTable("orders", out _).Should().BeFalse("an unqualified duplicate is ambiguous");
                engine.Catalog.TryGetTable(new CatalogTableName("app", "sales", "orders"), out var table)
                    .Should().BeTrue();
                table!.QualifiedName.QualifiedName.Should().Be("[app].[sales].[orders]");
            }

            await using var reopened = await StorageEngine.OpenAsync(path);
            reopened.Catalog.TryGetTable(new CatalogTableName("app", "archive", "orders"), out var archived)
                .Should().BeTrue();
            archived!.SchemaName.Should().Be("archive");
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public async Task CompositeIndex_SupportsPrefixAndOpenEndedRanges()
    {
        var path = TemporaryPath();
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path);
            var table = await engine.CreateTableAsync("events", [
                new CatalogColumn(new ColumnId(1), "tenant", SqlType.Int, false),
                new CatalogColumn(new ColumnId(2), "sequence", SqlType.Int, false),
                new CatalogColumn(new ColumnId(3), "value", SqlType.NVarChar(20), false)]);
            var index = await engine.CreateIndexAsync("by_tenant_sequence", table.Id, false, [
                new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.Last),
                new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending, NullSortOrder.Last)]);
            var rows = await engine.OpenTableAsync(table.Id);
            foreach (var values in new[] { (1, 1), (1, 2), (2, 1), (3, 1) })
                await rows.InsertAsync(new Row([SqlValue.Integer(values.Item1), SqlValue.Integer(values.Item2),
                    SqlValue.Text($"{values.Item1}:{values.Item2}")]));

            var storageIndex = await engine.OpenIndexAsync(index.Id);
            var tenantOne = await CollectAsync(storageIndex.ScanAsync(new StorageIndexRange(
                new StorageIndexBound([SqlValue.Integer(1)]),
                new StorageIndexBound([SqlValue.Integer(1)]))));
            tenantOne.Should().HaveCount(2);
            var afterTenantOne = await CollectAsync(storageIndex.ScanAsync(new StorageIndexRange(
                new StorageIndexBound([SqlValue.Integer(1)], inclusive: false))));
            afterTenantOne.Should().HaveCount(2);
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public async Task IncludedColumns_AreReturnedWithoutHeapFetchAndStayCurrent()
    {
        var path = TemporaryPath();
        try
        {
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                var table = await engine.CreateTableAsync("items", [
                    new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
                    new CatalogColumn(new ColumnId(2), "name", SqlType.NVarChar(50), false)]);
                var index = await engine.CreateIndexAsync("items_cover", table.Id, true,
                    [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.Last)],
                    new CatalogBTreeIndexOptions(includedColumns: [new ColumnId(2)]));
                var rows = await engine.OpenTableAsync(table.Id);
                var rowId = await rows.InsertAsync(new Row([SqlValue.Integer(7), SqlValue.Text("before")]));
                var storageIndex = await engine.OpenIndexAsync(index.Id);
                (await storageIndex.FindEntriesAsync([SqlValue.Integer(7)])).Single().IncludedValues[new ColumnId(2)]
                    .Should().Be(SqlValue.Text("before"));

                await rows.UpdateAsync(rowId, new RowUpdate([new ColumnUpdate(1, SqlValue.Text("after"))]));
                (await storageIndex.FindEntriesAsync([SqlValue.Integer(7)])).Single().IncludedValues[new ColumnId(2)]
                    .Should().Be(SqlValue.Text("after"));
            }

            await using var reopened = await StorageEngine.OpenAsync(path);
            reopened.Catalog.TryGetTable("items", out var tableDefinition).Should().BeTrue();
            var indexDefinition = reopened.Catalog.GetIndexes(tableDefinition!.Id).Single();
            var covered = await (await reopened.OpenIndexAsync(indexDefinition.Id))
                .FindEntriesAsync([SqlValue.Integer(7)]);
            covered.Single().IncludedValues[new ColumnId(2)].Should().Be(SqlValue.Text("after"));
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public async Task StatisticsAndStatementAtomicity_AreExposedAtHighLevel()
    {
        var path = TemporaryPath();
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path);
            var table = await engine.CreateTableAsync("numbers", Columns());
            var index = await engine.CreateIndexAsync("numbers_by_id", table.Id, true,
                [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.Last)]);

            await ((Func<Task>)(async () => await engine.ExecuteStatementAsync(async (statement, token) =>
            {
                var rows = await statement.OpenTableAsync(table.Id, token);
                await rows.InsertAsync(new Row([SqlValue.Integer(1)]), token);
                await rows.InsertAsync(new Row([SqlValue.Integer(2)]), token);
                throw new InvalidOperationException("abort statement");
            }))).Should().ThrowAsync<InvalidOperationException>();
            (await engine.GetTableStatisticsAsync(table.Id)).RowCount.Should().Be(0);

            await engine.ExecuteStatementAsync(async (statement, token) =>
            {
                var rows = await statement.OpenTableAsync(table.Id, token);
                await rows.InsertAsync(new Row([SqlValue.Integer(1)]), token);
                await rows.InsertAsync(new Row([SqlValue.Integer(2)]), token);
            });
            var tableStatistics = await engine.GetTableStatisticsAsync(table.Id);
            tableStatistics.RowCount.Should().Be(2);
            tableStatistics.HeapPageCount.Should().BeGreaterThan(0);
            tableStatistics.Columns.Single().DistinctValueCount.Should().Be(2);
            var indexStatistics = await engine.GetIndexStatisticsAsync(index.Id);
            indexStatistics.EntryCount.Should().Be(2);
            indexStatistics.DistinctKeyCount.Should().Be(2);
            indexStatistics.Histogram.Should().HaveCount(2);
            File.Exists(path + ".statement-undo").Should().BeFalse();
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public async Task ActiveStatementJournal_IsRecoveredBeforeDatabaseOpen()
    {
        var path = TemporaryPath();
        try
        {
            CatalogTable table;
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                table = await engine.CreateTableAsync("recovery", Columns());
                var rows = await engine.OpenTableAsync(table.Id);
                await rows.InsertAsync(new Row([SqlValue.Integer(1)]));
                await using var journal = await StatementJournal.CreateAsync(path, CancellationToken.None);
                await rows.InsertAsync(new Row([SqlValue.Integer(2)]));
            }

            File.Exists(path + ".statement-undo").Should().BeTrue();
            await using var recovered = await StorageEngine.OpenAsync(path);
            var values = new List<long>();
            await foreach (var row in (await recovered.OpenTableAsync(table.Id)).ScanAsync())
                values.Add(((IntegerSqlValue)row.Row.Values[0]).Value);
            values.Should().Equal(1);
            File.Exists(path + ".statement-undo").Should().BeFalse();
        }
        finally { DeleteDatabase(path); }
    }

    private static CatalogColumn[] Columns() =>
        [new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false)];

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        List<T> result = [];
        await foreach (var item in source) result.Add(item);
        return result;
    }

    private static string TemporaryPath() => Path.Combine(Path.GetTempPath(), $"storage-gaps-{Guid.NewGuid():N}.db");
    private static void DeleteDatabase(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".statement-undo")) File.Delete(path + ".statement-undo");
    }
}
