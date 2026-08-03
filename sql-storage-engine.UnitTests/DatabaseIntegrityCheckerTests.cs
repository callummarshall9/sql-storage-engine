using AwesomeAssertions;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;

public sealed class DatabaseIntegrityCheckerTests
{
    [Test]
    public async Task HealthyDatabase_HasNoFindingsAndCheckerDoesNotModifyBytes()
    {
        await using var store = new InMemoryPageStore(reservePageZero: false);
        await store.AllocateAsync(PageType.DatabaseHeader);
        var header = Header(1);
        var bytes = new byte[store.PageSize]; DatabaseHeaderCodec.Write(bytes, header);
        await store.WriteAsync(new PageId(0), bytes);
        var before = bytes.ToArray();

        var report = await new DatabaseIntegrityChecker().CheckAsync(store, header);

        report.IsHealthy.Should().BeTrue();
        var after = new byte[store.PageSize]; await store.ReadAsync(new PageId(0), after);
        after.Should().Equal(before);
    }

    [Test]
    public async Task InjectedCorruption_ProducesStableMachineReadableCode()
    {
        await using var store = new InMemoryPageStore(reservePageZero: false);
        await store.AllocateAsync(PageType.DatabaseHeader);
        var header = Header(1);
        var bytes = new byte[store.PageSize]; DatabaseHeaderCodec.Write(bytes, header); bytes[^1] ^= 1;
        await store.WriteAsync(new PageId(0), bytes);
        var report = await new DatabaseIntegrityChecker().CheckAsync(store, header);
        report.Findings.Should().ContainSingle(finding => finding.Code == "PAGE_CORRUPTION" && finding.PageId == new PageId(0));
    }

    [Test]
    public async Task CrossCheck_DetectsMissingAndStaleIndexRows()
    {
        await using var store = new InMemoryPageStore(reservePageZero: false);
        await store.AllocateAsync(PageType.DatabaseHeader);
        var header = Header(1); var bytes = new byte[store.PageSize]; DatabaseHeaderCodec.Write(bytes, header);
        await store.WriteAsync(new PageId(0), bytes);
        var missing = Row(1); var stale = Row(2);
        var report = await new DatabaseIntegrityChecker().CheckAsync(store, header,
            new IntegrityCrossCheck(new HashSet<RowId> { missing }, new HashSet<RowId> { stale }));
        report.Findings.Select(finding => finding.Code).Should().BeEquivalentTo(["INDEX_ENTRY_MISSING", "INDEX_ENTRY_STALE"]);
    }

    [Test]
    public async Task TraversalBound_StopsBeforeReadingFileControlledPageCount()
    {
        await using var store = new InMemoryPageStore();
        var report = await new DatabaseIntegrityChecker(2).CheckAsync(store, Header(3));
        report.Findings.Should().ContainSingle(finding => finding.Code == "TRAVERSAL_LIMIT");
    }

    private static DatabaseHeader Header(ulong next) => new(DatabaseId.New(), PageConstants.DefaultSize,
        DatabaseHeader.CurrentFormatVersion, null, null, new TableId(1), new IndexId(1), new TransactionId(1),
        new PageId(next), true);
    private static RowId Row(ulong page) => new(new PageId(page), new SlotId(0), new SlotGeneration(1));
}
