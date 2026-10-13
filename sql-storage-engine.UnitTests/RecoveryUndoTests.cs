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
public sealed class RecoveryUndoTests
{
    [TestCase(PageType.Heap)]
    [TestCase(PageType.BPlusTreeLeaf)]
    [TestCase(PageType.Catalog)]
    [TestCase(PageType.Overflow)]
    public async Task IncompletePhysicalMutationsAreUndoneAndEndWithRollback(PageType type)
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var page = await store.AllocateAsync(type);
        var before = Image(page,type,0,1);
        var after = Image(page,type,10,2);
        await store.WriteAsync(page,after);
        var record = Change(page,type,before,after,10,1,default);
        var device = new WriteAheadLogTests.MemoryWalDevice();
        var wal = await sql_storage_engine.Exposures.Logging.WriteAheadLogExposure.OpenAsync(device);
        var completed = await (wal).ApplyRecoveryUndoAsync(store,Analysis(record,TransactionState.Active));
        completed.Should().BeTrue();
        (await Read(store,page)).Should().Equal(before);
        WalReadResultExposure.ReadWalReadResult(device.Bytes).Records.Select(item => item.Type).Should().Equal(WalRecordType.Compensation, WalRecordType.Rollback);
    }

    [Test]
    public async Task CommittedTransactionIsNeverUndone()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var page = await store.AllocateAsync(PageType.Heap);
        var before = Image(page,PageType.Heap,0,1);
        var after = Image(page,PageType.Heap,10,2);
        await store.WriteAsync(page,after);
        var device = new WriteAheadLogTests.MemoryWalDevice();
        await (await sql_storage_engine.Exposures.Logging.WriteAheadLogExposure.OpenAsync(device)).ApplyRecoveryUndoAsync(store,Analysis(Change(page,PageType.Heap,before,after,10,1,default),TransactionState.Committed));
        (await Read(store,page)).Should().Equal(after);
        device.Bytes.Should().BeEmpty();
    }

    [Test]
    public async Task InterruptedUndoCanResumeFromCompensationLink()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var first = await store.AllocateAsync(PageType.Heap);
        var second = await store.AllocateAsync(PageType.Heap);
        var firstBefore = Image(first,PageType.Heap,0,1);
        var firstAfter = Image(first,PageType.Heap,10,2);
        var secondBefore = Image(second,PageType.Heap,0,3);
        var secondAfter = Image(second,PageType.Heap,20,4);
        await store.WriteAsync(first,firstAfter);
        await store.WriteAsync(second,secondAfter);
        var records = new[]
        {
            Change(first,PageType.Heap,firstBefore,firstAfter,10,1,default),
            Change(second,PageType.Heap,secondBefore,secondAfter,20,1,new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 10 })
        };
        var device = new WriteAheadLogTests.MemoryWalDevice();
        foreach (var record in records)
            device.AppendRaw(WalRecordExposure.WriteWalRecord(record));
        var wal = await sql_storage_engine.Exposures.Logging.WriteAheadLogExposure.OpenAsync(device);
        (await (wal).ApplyRecoveryUndoAsync(store,Analysis(records, TransactionState.Active),1)).Should().BeFalse();
        var rescanned = WalReadResultExposure.ReadWalReadResult(device.Bytes).Records;
        await (await sql_storage_engine.Exposures.Logging.WriteAheadLogExposure.OpenAsync(device)).ApplyRecoveryUndoAsync(store,Analysis(rescanned, TransactionState.Active));
        (await Read(store,first)).Should().Equal(firstBefore);
        (await Read(store,second)).Should().Equal(secondBefore);
        WalReadResultExposure.ReadWalReadResult(device.Bytes).Records[^1].Type.Should().Be(WalRecordType.Rollback);
    }

    private static RecoveryAnalysis Analysis(WalRecord record, TransactionState state) => Analysis([record], state);
    private static RecoveryAnalysis Analysis(IReadOnlyList<WalRecord> records, TransactionState state) => new sql_storage_engine.Models.Logging.RecoveryAnalysis
    {
        Transactions = new Dictionary<TransactionId, TransactionState>
        {
            [new sql_storage_engine.Models.Identifiers.TransactionId { Value = 1 }] = state
        },
        DirtyPages = new Dictionary<PageId, LogSequenceNumber>(),
        Records = records,
        ValidLength = 0,
        TruncatedTail = false
    };
    private static WalRecord Change(PageId id, PageType type, byte[] before, byte[] after, ulong lsn, ulong tx, LogSequenceNumber previous) => new sql_storage_engine.Models.Logging.WalRecord
    {
        Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
        {
            Value = lsn
        },
        PreviousLsn = previous,
        TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
        {
            Value = tx
        },
        Type = WalRecordType.PageChange,
        Payload = PhysicalPageChangeExposure.WritePhysicalPageChange(new sql_storage_engine.Models.Logging.PhysicalPageChange { PageId = id, PageType = type, BeforeImage = before, AfterImage = after })
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