// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Exposures.Logging;
using AwesomeAssertions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Pages;
using sql_storage_engine.Transactions;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class RecoveryRedoTests
{
    [Test]
    public async Task MissingCommittedChangeIsRestoredAndRepeatedRedoIsIdempotent()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var page = await store.AllocateAsync(PageType.Heap);
        var before = await Read(store,page);
        var after = Image(page,PageType.Heap,10,77);
        var analysis = Analysis(new WalRecord { Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 10 }, PreviousLsn = default, TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId { Value = 1 }, Type = WalRecordType.PageChange, Payload = PhysicalPageChangeExposure.WritePhysicalPageChange(new sql_storage_engine.Models.Logging.PhysicalPageChange { PageId = page, PageType = PageType.Heap, BeforeImage = before, AfterImage = after }) });
        await sql_storage_engine.Exposures.Logging.WriteAheadLogExposure.ApplyRecoveryRedoAsync(store,analysis);
        (await Read(store,page)).Should().Equal(after);
        await sql_storage_engine.Exposures.Logging.WriteAheadLogExposure.ApplyRecoveryRedoAsync(store,analysis);
        (await Read(store,page)).Should().Equal(after);
    }

    [Test]
    public async Task AlreadyAppliedNewerPageIsSkipped()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var page = await store.AllocateAsync(PageType.Heap);
        var current = Image(page,PageType.Heap,20,9);
        await store.WriteAsync(page,current);
        var record = new WalRecord
        {
            Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = 10
            },
            PreviousLsn = default,
            TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
            {
                Value = 1
            },
            Type = WalRecordType.PageChange,
            Payload = PhysicalPageChangeExposure.WritePhysicalPageChange(new sql_storage_engine.Models.Logging.PhysicalPageChange { PageId = page, PageType = PageType.Heap, BeforeImage = current, AfterImage = Image(page,PageType.Heap,10,4) })
        };
        await sql_storage_engine.Exposures.Logging.WriteAheadLogExposure.ApplyRecoveryRedoAsync(store,Analysis(record));
        (await Read(store,page)).Should().Equal(current);
    }

    [Test]
    public async Task CorruptPageIsRepairedFromVerifiedImageButWrongTypeStopsRecovery()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var page = await store.AllocateAsync(PageType.Heap);
        var before = await Read(store,page);
        var corrupt = before.ToArray();
        corrupt[^1] ^= 1;
        await store.WriteAsync(page, corrupt);
        var record = new WalRecord
        {
            Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = 10
            },
            PreviousLsn = default,
            TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
            {
                Value = 1
            },
            Type = WalRecordType.PageChange,
            Payload = PhysicalPageChangeExposure.WritePhysicalPageChange(new sql_storage_engine.Models.Logging.PhysicalPageChange { PageId = page, PageType = PageType.Heap, BeforeImage = before, AfterImage = Image(page,PageType.Heap,10,4) })
        };
        await sql_storage_engine.Exposures.Logging.WriteAheadLogExposure.ApplyRecoveryRedoAsync(store,Analysis(record));
        var wrong = record with
        {
            Payload = PhysicalPageChangeExposure.WritePhysicalPageChange(new sql_storage_engine.Models.Logging.PhysicalPageChange { PageId = page, PageType = PageType.Catalog, BeforeImage = before, AfterImage = Image(page,PageType.Catalog,10,4) })
        };
        await ((Func<Task>)(async () => await sql_storage_engine.Exposures.Logging.WriteAheadLogExposure.ApplyRecoveryRedoAsync(store,Analysis(wrong)))).Should().ThrowAsync<Exception>();
    }

    private static RecoveryAnalysis Analysis(WalRecord record) => new sql_storage_engine.Models.Logging.RecoveryAnalysis
    {
        Transactions = new Dictionary<TransactionId, TransactionState>
        {
            [record.TransactionId] = TransactionState.Committed
        },
        DirtyPages = new Dictionary<PageId, LogSequenceNumber>(),
        Records = [record],
        ValidLength = 0,
        TruncatedTail = false
    };
    private static async Task<byte[]> Read(sql_storage_engine.Exposures.Pages.IPageStoreExposure store, PageId id)
    {
        var bytes = new byte[store.GetPageSize()];
        await store.ReadAsync(id,bytes);
        return bytes;
    }

    private static byte[] Image(PageId id, PageType type, ulong lsn, byte marker)
    {
        var bytes = new byte[sql_storage_engine.Models.Pages.PageConstants.DefaultSize];
        sql_storage_engine.Exposures.Pages.PageHeaderExposure.WritePageHeader(bytes,new sql_storage_engine.Models.Pages.PageHeader { PageId = id, PageType = type, FormatVersion = new sql_storage_engine.Models.Pages.PageFormatVersion { Value = sql_storage_engine.Models.Pages.PageHeaderFormat.CurrentVersion }, PageLogSequenceNumber = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = lsn }, ChecksumAlgorithm = PageChecksumAlgorithm.Crc32, Checksum = 0 });
        bytes[^1] = marker;
        PageChecksumExposure.WriteChecksum(bytes,bytes.Length);
        return bytes;
    }
}