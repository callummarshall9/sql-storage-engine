// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Exposures.Catalog;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Exposures.Storage;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.UnitTests;
public sealed class StorageSessionExposureTests
{
    [Test]
    public async Task UnifiedSessionPersistsCatalogAndOverflowRowsAcrossReopen()
    {
        var directory = Path.Combine(Path.GetTempPath(), "unified-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "database.db");
        try
        {
            TableId tableId;
            RowId rowId;
            var text = new string('x', 12000);
            await using (var engine = await StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var definition = await engine.CreateCatalogTableAsync("payloads", [CatalogColumnExposure.CreateCatalogColumn(new ColumnId { Value = 1 }, "payload", SqlTypeExposure.CreateVarCharMaxSqlType(), false)]);
                tableId = definition.Id;
                var table = await engine.OpenTableAsync(tableId);
                rowId = await table.InsertAsync(RowExposure.CreateRow([SqlValueExposure.CreateTextSqlValue(text)]));
                ((TextSqlValue)(await table.GetAsync(rowId))!.Row.Values[0]).Value.Should().Be(text);
                await engine.FlushAsync();
            }
            await using (var reopened = await StorageSessionExposure.OpenStorageSessionExposureAsync(path))
            {
                reopened.ReadTables().Should().ContainSingle();
                var table = await reopened.OpenTableAsync(tableId);
                ((TextSqlValue)(await table.GetAsync(rowId))!.Row.Values[0]).Value.Should().Be(text);
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
