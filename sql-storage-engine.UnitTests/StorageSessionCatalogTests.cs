// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Catalog;
using sql_storage_engine.Exposures.Catalog;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Exposures.Storage;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class StorageSessionCatalogTests
{
    [Test]
    public async Task ExistingTableHandlesUseNewIndexesAndStatisticsUseConfiguredClockAndCollation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "session-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "database.db");
        var timestamp = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var options = StorageQueryExposure.CreateStorageEngineOptions() with { DefaultCollation = "Latin1_General_100_BIN2", TimeProvider = new FixedClock(timestamp) };
        try
        {
            await using var engine = await StorageSessionExposure.CreateStorageSessionExposureAsync(path: path, storageEngineOptions: options);
            var definition = await engine.CreateCatalogTableAsync(name: "names", columns: [CatalogColumnExposure.CreateCatalogColumn(columnId: new ColumnId { Value = 1 }, name: "name", sqlType: SqlTypeExposure.CreateNVarCharSqlType(length: 20), isNullable: true)]);
            Assert.That(definition.Columns[0].Type.Collation, Is.EqualTo(options.DefaultCollation));
            var table = await engine.OpenTableAsync(tableId: definition.Id);
            await table.InsertAsync(row: RowExposure.CreateRow(values: [SqlValueExposure.CreateTextSqlValue(value: "alpha")]));
            var index = await engine.CreateCatalogIndexAsync(name: "names_index", tableId: definition.Id, isUnique: false, columns: [CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(columnId: new ColumnId { Value = 1 }, direction:SortDirection.Ascending, nullSortOrder:NullSortOrder.Last)]);
            await table.InsertAsync(row: RowExposure.CreateRow(values: [SqlValueExposure.CreateTextSqlValue(value: "beta")]));
            var handle = await engine.OpenIndexAsync(indexId: index.Id);
            Assert.That(await handle.FindAsync(values: [SqlValueExposure.CreateTextSqlValue(value: "beta")]), Has.Count.EqualTo(1));
            var tableStatistics = await engine.GetTableStatisticsAsync(tableId: definition.Id);
            var indexStatistics = await engine.GetIndexStatisticsAsync(indexId: index.Id);
            Assert.Multiple(() =>
            {
                Assert.That(tableStatistics.RowCount, Is.EqualTo(2));
                Assert.That(tableStatistics.Columns[0].DistinctValueCount, Is.EqualTo(2));
                Assert.That(tableStatistics.CapturedAt, Is.EqualTo(timestamp));
                Assert.That(indexStatistics.EntryCount, Is.EqualTo(2));
                Assert.That(indexStatistics.DistinctKeyCount, Is.EqualTo(2));
                Assert.That(indexStatistics.Histogram, Has.Count.EqualTo(2));
                Assert.That(indexStatistics.CapturedAt, Is.EqualTo(timestamp));
            });
        }
        finally
        {
            Directory.Delete(path: directory, recursive: true);
        }
    }

    private sealed class FixedClock(DateTimeOffset timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => timestamp;
    }
}