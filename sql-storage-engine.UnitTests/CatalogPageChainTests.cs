using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class CatalogPageChainTests
{
    [Test]
    public async Task MultiPageCatalog_TraversesAndRoundTrips()
    {
        await using var pages = new InMemoryPageStore(PageConstants.MinimumSize);
        var chain = new CatalogPageChain(pages, pages);
        var columns = Enumerable.Range(1, 180)
            .Select(value => new CatalogColumn(new ColumnId((ulong)value), $"column_{value:D3}", SqlType.Text, true));
        var catalog = new CatalogDefinition(
            [new CatalogTable(new TableId(1), "large", 1, new PageId(500), columns)], []);
        var written = await chain.WriteAsync(catalog);
        var reopened = await chain.ReadAsync(written.RootPageId);
        written.PageIds.Should().HaveCountGreaterThan(1);
        reopened.Tables.Single().Columns.Should().HaveCount(180);
    }

    [Test]
    public async Task CorruptCatalogPage_IsRejectedBeforeRecordsAreDecoded()
    {
        await using var pages = new InMemoryPageStore();
        var chain = new CatalogPageChain(pages, pages);
        var catalog = new CatalogDefinition([new CatalogTable(new TableId(1), "t", 1, new PageId(2),
            [new CatalogColumn(new ColumnId(1), "c", SqlType.Integer, false)])], []);
        var written = await chain.WriteAsync(catalog);
        var page = new byte[pages.PageSize];
        await pages.ReadAsync(written.RootPageId, page);
        page[^1] ^= 1;
        await pages.WriteAsync(written.RootPageId, page);
        await ((Func<Task>)(async () => await chain.ReadAsync(written.RootPageId))).Should()
            .ThrowAsync<StorageCorruptionException>();
    }
}
