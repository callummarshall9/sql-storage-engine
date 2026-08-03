using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Pages;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;

public sealed class RecoveryUndoTests
{
    [TestCase(PageType.Heap)] [TestCase(PageType.BPlusTreeLeaf)] [TestCase(PageType.Catalog)] [TestCase(PageType.Overflow)]
    public async Task IncompletePhysicalMutationsAreUndoneAndEndWithRollback(PageType type)
    {
        await using var store = new InMemoryPageStore(); var page = await store.AllocateAsync(type);
        var before = Image(page, type, 0, 1); var after = Image(page, type, 10, 2); await store.WriteAsync(page, after);
        var record = Change(page, type, before, after, 10, 1, default);
        var device = new WriteAheadLogTests.MemoryWalDevice(); var wal = await WriteAheadLog.OpenAsync(device);
        var completed = await RecoveryUndo.ApplyAsync(store, Analysis(record, TransactionState.Active), wal);
        completed.Should().BeTrue(); (await Read(store, page)).Should().Equal(before);
        WalFormat.ReadRecords(device.Bytes).Records.Select(item => item.Type).Should()
            .Equal(WalRecordType.Compensation, WalRecordType.Rollback);
    }

    [Test]
    public async Task CommittedTransactionIsNeverUndone()
    {
        await using var store = new InMemoryPageStore(); var page = await store.AllocateAsync(PageType.Heap);
        var before = Image(page, PageType.Heap, 0, 1); var after = Image(page, PageType.Heap, 10, 2); await store.WriteAsync(page, after);
        var device = new WriteAheadLogTests.MemoryWalDevice();
        await RecoveryUndo.ApplyAsync(store, Analysis(Change(page, PageType.Heap, before, after, 10, 1, default),
            TransactionState.Committed), await WriteAheadLog.OpenAsync(device));
        (await Read(store, page)).Should().Equal(after); device.Bytes.Should().BeEmpty();
    }

    [Test]
    public async Task InterruptedUndoCanResumeFromCompensationLink()
    {
        await using var store = new InMemoryPageStore(); var first = await store.AllocateAsync(PageType.Heap);
        var second = await store.AllocateAsync(PageType.Heap);
        var firstBefore = Image(first, PageType.Heap, 0, 1); var firstAfter = Image(first, PageType.Heap, 10, 2);
        var secondBefore = Image(second, PageType.Heap, 0, 3); var secondAfter = Image(second, PageType.Heap, 20, 4);
        await store.WriteAsync(first, firstAfter); await store.WriteAsync(second, secondAfter);
        var records = new[] { Change(first, PageType.Heap, firstBefore, firstAfter, 10, 1, default),
            Change(second, PageType.Heap, secondBefore, secondAfter, 20, 1, new LogSequenceNumber(10)) };
        var device = new WriteAheadLogTests.MemoryWalDevice();
        foreach (var record in records) device.AppendRaw(WalFormat.WriteRecord(record));
        var wal = await WriteAheadLog.OpenAsync(device);
        (await RecoveryUndo.ApplyAsync(store, Analysis(records, TransactionState.Active), wal, 1)).Should().BeFalse();
        var rescanned = WalFormat.ReadRecords(device.Bytes).Records;
        await RecoveryUndo.ApplyAsync(store, Analysis(rescanned, TransactionState.Active), await WriteAheadLog.OpenAsync(device));
        (await Read(store, first)).Should().Equal(firstBefore); (await Read(store, second)).Should().Equal(secondBefore);
        WalFormat.ReadRecords(device.Bytes).Records[^1].Type.Should().Be(WalRecordType.Rollback);
    }

    private static RecoveryAnalysis Analysis(WalRecord record, TransactionState state) => Analysis([record], state);
    private static RecoveryAnalysis Analysis(IReadOnlyList<WalRecord> records, TransactionState state) => new(
        new Dictionary<TransactionId, TransactionState> { [new TransactionId(1)] = state }, new Dictionary<PageId, LogSequenceNumber>(), records, 0, false);
    private static WalRecord Change(PageId id, PageType type, byte[] before, byte[] after, ulong lsn, ulong tx, LogSequenceNumber previous) =>
        new(new LogSequenceNumber(lsn), previous, new TransactionId(tx), WalRecordType.PageChange,
            PhysicalPageChangeCodec.Write(new(id, type, before, after)));
    private static async Task<byte[]> Read(IPageStore store, PageId id) { var bytes = new byte[store.PageSize]; await store.ReadAsync(id, bytes); return bytes; }
    private static byte[] Image(PageId id, PageType type, ulong lsn, byte marker)
    { var bytes = new byte[PageConstants.DefaultSize]; PageHeaderCodec.Write(bytes, new(id, type, PageFormatVersion.Current,
        new LogSequenceNumber(lsn), PageChecksumAlgorithm.Crc32, 0)); bytes[^1] = marker; PageChecksum.WriteChecksum(bytes, bytes.Length); return bytes; }
}
