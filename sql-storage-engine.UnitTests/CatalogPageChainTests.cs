using AwesomeAssertions;
using sql_storage_engine.Exposures.Catalog;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class CatalogPageChainTests
{
    [Test]
    public async Task MultiPageCatalog_TraversesAndRoundTripsOpaqueBytes()
    {
        await using var pages = new MemoryPageStoreExposure(PageConstants.MinimumSize);
        var chain = new CatalogPageChainExposure(pages, pages);
        var payload = new byte[3 * PageConstants.MinimumSize + 137];
        new Random(42).NextBytes(payload);

        var written = await chain.WriteAsync(payload);
        var reopened = await chain.ReadAsync(written.RootPageId);

        written.PageIds.Should().HaveCountGreaterThan(1);
        reopened.Should().Equal(payload);
    }

    [Test]
    public async Task CorruptCatalogPage_IsRejectedBeforePayloadIsReturned()
    {
        await using var pages = new MemoryPageStoreExposure();
        var chain = new CatalogPageChainExposure(pages, pages);
        var written = await chain.WriteAsync([1, 2, 3, 4]);
        var page = new byte[pages.GetPageSize()];
        await pages.ReadAsync(written.RootPageId, page);
        page[^1] ^= 1;
        await pages.WriteAsync(written.RootPageId, page);

        await ((Func<Task>)(async () => await chain.ReadAsync(written.RootPageId)))
            .Should().ThrowAsync<StorageCorruptionException>();
    }
}
