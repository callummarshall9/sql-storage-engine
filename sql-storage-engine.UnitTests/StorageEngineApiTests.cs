// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Rows;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;
using sql_storage_engine.Models.Identifiers;

using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class StorageEngineApiTests
{
    [Test]
    public async Task PublicApi_SupportsCatalogBindingAndLogicalRowAccessAcrossReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"storage-api-{Guid.NewGuid():N}.db");
        try
        {
            await using (var engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path, sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageEngineOptions()with{BufferPoolCapacity = 16, InlineValueThreshold = 8 }))
            {
                var table = await engine.CreateCatalogTableAsync("items", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "name", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(4000), true)]);
                var index = await engine.CreateCatalogIndexAsync("items_by_id", table.Id, true, [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.Last)]);
                engine.ReadCatalog().TryGetTable("items", out var bound).Should().BeTrue();
                bound!.Columns.Select(column => column.Name).Should().Equal("id", "name");
                var rows = await engine.OpenTableAsync(table.Id);
                var rowId = await rows.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("a long value")]));
                (await rows.GetAsync(rowId))!.Row.Values.Should().Equal(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("a long value"));
                var scanned = new List<StoredRow>();
                await foreach (var row in rows.ScanAsync())
                    scanned.Add(row);
                scanned.Select(row => row.RowId).Should().Equal(rowId);
                var lookup = await engine.OpenIndexAsync(index.Id);
                (await lookup.FindAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7)])).Should().Equal(rowId);
            }

            await using (var reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path, sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageEngineOptions()with{BufferPoolCapacity = 16, InlineValueThreshold = 8 }))
            {
                reopened.ReadCatalog().TryGetTable("items", out var table).Should().BeTrue();
                var rows = await reopened.OpenTableAsync(table!.Id);
                var scanned = new List<StoredRow>();
                await foreach (var row in rows.ScanAsync())
                    scanned.Add(row);
                scanned.Should().ContainSingle();
                ((IntegerSqlValue)scanned[0].Row.Values[0]).Value.Should().Be(7);
            }
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}