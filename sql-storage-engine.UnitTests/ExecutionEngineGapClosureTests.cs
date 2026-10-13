// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Transactions;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class ExecutionEngineGapClosureTests
{
    [Test]
    public async Task DatabaseIncarnationIsPublicStableAcrossReopenAndUniqueAcrossFiles()
    {
        var firstPath = TemporaryPath();
        var secondPath = TemporaryPath();
        try
        {
            DatabaseId incarnation;
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure first = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(firstPath))
            {
                incarnation = first.ReadDatabaseId();
                first.ReadCatalog().ReadDatabaseId().Should().Be(incarnation);
            }

            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(firstPath))
                reopened.ReadDatabaseId().Should().Be(incarnation);
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure second = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(secondPath))
                second.ReadDatabaseId().Should().NotBe(incarnation);
        }
        finally
        {
            DeleteDatabase(firstPath);
            DeleteDatabase(secondPath);
        }
    }

    [Test]
    public async Task StatementCanAtomicallyCreateAndPopulateATable()
    {
        var path = TemporaryPath();
        try
        {
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            await ((Func<Task>)(async () => await engine.ExecuteStatementAsync(async (statement, token) =>
            {
                var table = await statement.CreateCatalogTableAsync(sql_storage_engine.Exposures.Catalog.CatalogTableNameExposure.CreateCatalogTableName("app","reports","failed_target"),Columns(),cancellationToken: token);
                await (await statement.OpenTableAsync(table.Id,token)).InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)]),token);
                throw new InvalidOperationException("fail after target population");
            }))).Should().ThrowAsync<InvalidOperationException>();
            engine.ReadCatalog().TryGetTable(sql_storage_engine.Exposures.Catalog.CatalogTableNameExposure.CreateCatalogTableName("app","reports","failed_target"),out _).Should().BeFalse();
            await engine.ExecuteStatementAsync(async (statement, token) =>
            {
                var table = await statement.CreateCatalogTableAsync(sql_storage_engine.Exposures.Catalog.CatalogTableNameExposure.CreateCatalogTableName("app","reports","target"),Columns(),cancellationToken: token);
                await (await statement.OpenTableAsync(table.Id,token)).InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7)]),token);
            });
            engine.ReadCatalog().TryGetTable(sql_storage_engine.Exposures.Catalog.CatalogTableNameExposure.CreateCatalogTableName("app","reports","target"),out var created).Should().BeTrue();
            var rows = await CollectAsync((await engine.OpenTableAsync(created!.Id)).ScanAsync());
            ((IntegerSqlValue)rows.Single().Row.Values[0]).Value.Should().Be(7);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public async Task QualifiedTables_AreUnambiguousAndSurviveReopen()
    {
        var path = TemporaryPath();
        try
        {
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                await engine.CreateCatalogTableAsync(sql_storage_engine.Exposures.Catalog.CatalogTableNameExposure.CreateCatalogTableName("app","sales","orders"),Columns());
                await engine.CreateCatalogTableAsync(sql_storage_engine.Exposures.Catalog.CatalogTableNameExposure.CreateCatalogTableName("app","archive","orders"),Columns());
                engine.ReadCatalog().TryGetTable("orders",out _).Should().BeFalse("an unqualified duplicate is ambiguous");
                engine.ReadCatalog().TryGetTable(sql_storage_engine.Exposures.Catalog.CatalogTableNameExposure.CreateCatalogTableName("app","sales","orders"),out var table).Should().BeTrue();
                sql_storage_engine.Exposures.Catalog.CatalogTableNameExposure.FormatCatalogTableName(table!.QualifiedName).Should().Be("[app].[sales].[orders]");
            }

            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            reopened.ReadCatalog().TryGetTable(sql_storage_engine.Exposures.Catalog.CatalogTableNameExposure.CreateCatalogTableName("app","archive","orders"),out var archived).Should().BeTrue();
            archived!.SchemaName.Should().Be("archive");
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public async Task CompositeIndex_SupportsPrefixAndOpenEndedRanges()
    {
        var path = TemporaryPath();
        try
        {
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var table = await engine.CreateCatalogTableAsync("events",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "tenant", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "sequence", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(20), false)]);
            var index = await engine.CreateCatalogIndexAsync("by_tenant_sequence",table.Id,false,[sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.Last), sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.Last)]);
            var rows = await engine.OpenTableAsync(table.Id);
            foreach (var values in new[]
            {
                (1, 1),
                (1, 2),
                (2, 1),
                (3, 1)
            }

            )
                await rows.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(values.Item1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(values.Item2), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue($"{values.Item1}:{values.Item2}")]));
            var storageIndex = await engine.OpenIndexAsync(index.Id);
            var tenantOne = await CollectAsync(storageIndex.ScanAsync(new StorageIndexRange { LowerBound = sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageIndexBound([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)]), UpperBound = sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageIndexBound([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)]), Direction = ScanDirection.Ascending }));
            tenantOne.Should().HaveCount(2);
            var afterTenantOne = await CollectAsync(storageIndex.ScanAsync(new StorageIndexRange { LowerBound = sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageIndexBound([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)], inclusive: false), UpperBound = null, Direction = ScanDirection.Ascending }));
            afterTenantOne.Should().HaveCount(2);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public async Task IncludedColumns_AreReturnedWithoutHeapFetchAndStayCurrent()
    {
        var path = TemporaryPath();
        try
        {
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var table = await engine.CreateCatalogTableAsync("items",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "name", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(50), false)]);
                var index = await engine.CreateCatalogIndexAsync("items_cover",table.Id,true,[sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.Last)],sql_storage_engine.Exposures.Catalog.CatalogBTreeIndexOptionsExposure.CreateCatalogBTreeIndexOptions(columnIds: [new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }]));
                var rows = await engine.OpenTableAsync(table.Id);
                var rowId = await rows.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("before")]));
                var storageIndex = await engine.OpenIndexAsync(index.Id);
                (await storageIndex.FindEntriesAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7)])).Single().IncludedValues[new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("before"));
                await rows.UpdateAsync(rowId,RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("after") }]));
                (await storageIndex.FindEntriesAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7)])).Single().IncludedValues[new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("after"));
            }

            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            reopened.ReadCatalog().TryGetTable("items",out var tableDefinition).Should().BeTrue();
            var indexDefinition = reopened.ReadCatalog().GetIndexes(tableDefinition!.Id).Single();
            var covered = await (await reopened.OpenIndexAsync(indexDefinition.Id)).FindEntriesAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7)]);
            covered.Single().IncludedValues[new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("after"));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public async Task StatisticsAndStatementAtomicity_AreExposedAtHighLevel()
    {
        var path = TemporaryPath();
        try
        {
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var table = await engine.CreateCatalogTableAsync("numbers",Columns());
            var index = await engine.CreateCatalogIndexAsync("numbers_by_id",table.Id,true,[sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.Last)]);
            await ((Func<Task>)(async () => await engine.ExecuteStatementAsync(async (statement, token) =>
            {
                var rows = await statement.OpenTableAsync(table.Id,token);
                await rows.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)]),token);
                await rows.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2)]),token);
                throw new InvalidOperationException("abort statement");
            }))).Should().ThrowAsync<InvalidOperationException>();
            (await engine.GetTableStatisticsAsync(table.Id)).RowCount.Should().Be(0);
            await engine.ExecuteStatementAsync(async (statement, token) =>
            {
                var rows = await statement.OpenTableAsync(table.Id,token);
                await rows.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)]),token);
                await rows.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2)]),token);
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
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public async Task ActiveStatementJournal_IsRecoveredBeforeDatabaseOpen()
    {
        var path = TemporaryPath();
        try
        {
            CatalogTable table;
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                table = await engine.CreateCatalogTableAsync("recovery",Columns());
                var rows = await engine.OpenTableAsync(table.Id);
                await rows.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)]));
                await using var journal = await sql_storage_engine.Exposures.Transactions.StatementJournalExposure.CreateStatementJournalExposureAsync(path, CancellationToken.None);
                await rows.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2)]));
            }

            File.Exists(path + ".statement-undo").Should().BeTrue();
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure recovered = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            var values = new List<long>();
            await foreach (var row in (await recovered.OpenTableAsync(table.Id)).ScanAsync())
                values.Add(((IntegerSqlValue)row.Row.Values[0]).Value);
            values.Should().Equal(1);
            File.Exists(path + ".statement-undo").Should().BeFalse();
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static CatalogColumn[] Columns() => [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)];
    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        List<T> result = [];
        await foreach (var item in source)
            result.Add(item);
        return result;
    }

    private static string TemporaryPath() => Path.Combine(Path.GetTempPath(), $"storage-gaps-{Guid.NewGuid():N}.db");
    private static void DeleteDatabase(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
        if (File.Exists(path + ".statement-undo"))
            File.Delete(path + ".statement-undo");
    }
}