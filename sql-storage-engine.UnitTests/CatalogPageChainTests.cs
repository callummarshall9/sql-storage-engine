using sql_storage_engine.Models.Catalog;
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

using sql_storage_engine.Models.Rows;
namespace sql_storage_engine.UnitTests;
public sealed class CatalogPageChainTests
{
    [Test]
    public async Task MultiPageCatalog_TraversesAndRoundTrips()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure pages = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure(sql_storage_engine.Models.Pages.PageConstants.MinimumSize);
        var chain = new sql_storage_engine.Exposures.Catalog.CatalogPageChainExposure(pages, pages);
        var columns = Enumerable.Range(1, 180).Select(value => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = (ulong)value }, $"column_{value:D3}", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(4000), true));
        var catalog = sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "large", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 500 }, columns)], []);
        var written = await chain.WriteAsync(sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Encode(catalog));
        var reopened = sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Decode(await chain.ReadAsync(written.RootPageId));
        written.PageIds.Should().HaveCountGreaterThan(1);
        reopened.Tables.Single().Columns.Should().HaveCount(180);
    }

    [Test]
    public async Task CorruptCatalogPage_IsRejectedBeforeRecordsAreDecoded()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure pages = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var chain = new sql_storage_engine.Exposures.Catalog.CatalogPageChainExposure(pages, pages);
        var catalog = sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "t", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 2 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "c", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(), false)])], []);
        var written = await chain.WriteAsync(sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Encode(catalog));
        var page = new byte[pages.GetPageSize()];
        await pages.ReadAsync(written.RootPageId,page);
        page[^1] ^= 1;
        await pages.WriteAsync(written.RootPageId,page);
        await ((Func<Task>)(async () => await chain.ReadAsync(written.RootPageId))).Should().ThrowAsync<StorageCorruptionException>();
    }
}