// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Exposures.Identifiers;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class DatabaseIntegrityCheckerTests
{
    [Test]
    public async Task HealthyDatabase_HasNoFindingsAndCheckerDoesNotModifyBytes()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure(reservePageZero: false);
        await store.AllocateAsync(PageType.DatabaseHeader);
        var header = Header(1);
        var bytes = new byte[store.GetPageSize()];
        sql_storage_engine.Exposures.Pages.DatabaseHeaderExposure.WriteDatabaseHeader(bytes,header);
        await store.WriteAsync(new sql_storage_engine.Models.Identifiers.PageId { Value = 0 },bytes);
        var before = bytes.ToArray();
        var report = await new sql_storage_engine.Exposures.Diagnostics.DatabaseIntegrityCheckerExposure(store: store).CheckAsync(databaseHeader: header);
        report.IsHealthy.Should().BeTrue();
        var after = new byte[store.GetPageSize()];
        await store.ReadAsync(new sql_storage_engine.Models.Identifiers.PageId { Value = 0 },after);
        after.Should().Equal(before);
    }

    [Test]
    public async Task InjectedCorruption_ProducesStableMachineReadableCode()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure(reservePageZero: false);
        await store.AllocateAsync(PageType.DatabaseHeader);
        var header = Header(1);
        var bytes = new byte[store.GetPageSize()];
        sql_storage_engine.Exposures.Pages.DatabaseHeaderExposure.WriteDatabaseHeader(bytes,header);
        bytes[^1] ^= 1;
        await store.WriteAsync(new sql_storage_engine.Models.Identifiers.PageId { Value = 0 },bytes);
        var report = await new sql_storage_engine.Exposures.Diagnostics.DatabaseIntegrityCheckerExposure(store: store).CheckAsync(databaseHeader: header);
        report.Findings.Should().ContainSingle(finding => finding.Code == "PAGE_CORRUPTION" && finding.PageId == new sql_storage_engine.Models.Identifiers.PageId { Value = 0 });
    }

    [Test]
    public async Task CrossCheck_DetectsMissingAndStaleIndexRows()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure(reservePageZero: false);
        await store.AllocateAsync(PageType.DatabaseHeader);
        var header = Header(1);
        var bytes = new byte[store.GetPageSize()];
        sql_storage_engine.Exposures.Pages.DatabaseHeaderExposure.WriteDatabaseHeader(bytes,header);
        await store.WriteAsync(new sql_storage_engine.Models.Identifiers.PageId { Value = 0 },bytes);
        var missing = Row(1);
        var stale = Row(2);
        var report = await new sql_storage_engine.Exposures.Diagnostics.DatabaseIntegrityCheckerExposure(store: store).CheckAsync(databaseHeader: header,integrityCrossCheck: new IntegrityCrossCheck { HeapRows = new HashSet<RowId> { missing }, IndexRows = new HashSet<RowId> { stale } });
        report.Findings.Select(finding => finding.Code).Should().BeEquivalentTo(["INDEX_ENTRY_MISSING", "INDEX_ENTRY_STALE"]);
    }

    [Test]
    public async Task TraversalBound_StopsBeforeReadingFileControlledPageCount()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var report = await new sql_storage_engine.Exposures.Diagnostics.DatabaseIntegrityCheckerExposure(store: store, maximumPages: 2).CheckAsync(databaseHeader: Header(3));
        report.Findings.Should().ContainSingle(finding => finding.Code == "TRAVERSAL_LIMIT");
    }

    private static DatabaseHeader Header(ulong next) => new sql_storage_engine.Models.Pages.DatabaseHeader
    {
        DatabaseId = DatabaseIdExposure.CreateDatabaseId(),
        PageSize = sql_storage_engine.Models.Pages.PageConstants.DefaultSize,
        FormatVersion = sql_storage_engine.Models.Pages.DatabaseHeaderFormat.CurrentVersion,
        CatalogRootPageId = null,
        FreeListHeadPageId = null,
        NextTableId = new sql_storage_engine.Models.Identifiers.TableId
        {
            Value = 1
        },
        NextIndexId = new sql_storage_engine.Models.Identifiers.IndexId
        {
            Value = 1
        },
        NextTransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
        {
            Value = 1
        },
        NextPageId = new sql_storage_engine.Models.Identifiers.PageId
        {
            Value = next
        },
        IsCleanShutdown = true,
        NextRowVersion = 1,
        DefaultCollation = "SQL_Latin1_General_CP1_CI_AS",
        NextGeneratedSequence = 1
    };
    private static RowId Row(ulong page) => new sql_storage_engine.Models.Identifiers.RowId
    {
        PageId = new sql_storage_engine.Models.Identifiers.PageId
        {
            Value = page
        },
        SlotId = new sql_storage_engine.Models.Identifiers.SlotId
        {
            Value = 0
        },
        Generation = new sql_storage_engine.Models.Identifiers.SlotGeneration
        {
            Value = 1
        }
    };
}