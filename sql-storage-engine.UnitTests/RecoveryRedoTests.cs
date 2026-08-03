using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Pages;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;

public sealed class RecoveryRedoTests
{
    [Test]
    public async Task MissingCommittedChangeIsRestoredAndRepeatedRedoIsIdempotent()
    {
        await using var store = new InMemoryPageStore();
        var page = await store.AllocateAsync(PageType.Heap);
        var before = await Read(store, page);
        var after = Image(page, PageType.Heap, 10, 77);
        var analysis = Analysis(new WalRecord(new LogSequenceNumber(10), default, new TransactionId(1),
            WalRecordType.PageChange, PhysicalPageChangeCodec.Write(new(page, PageType.Heap, before, after))));
        await RecoveryRedo.ApplyAsync(store, analysis);
        (await Read(store, page)).Should().Equal(after);
        await RecoveryRedo.ApplyAsync(store, analysis);
        (await Read(store, page)).Should().Equal(after);
    }

    [Test]
    public async Task AlreadyAppliedNewerPageIsSkipped()
    {
        await using var store = new InMemoryPageStore(); var page = await store.AllocateAsync(PageType.Heap);
        var current = Image(page, PageType.Heap, 20, 9); await store.WriteAsync(page, current);
        var record = new WalRecord(new LogSequenceNumber(10), default, new TransactionId(1), WalRecordType.PageChange,
            PhysicalPageChangeCodec.Write(new(page, PageType.Heap, current, Image(page, PageType.Heap, 10, 4))));
        await RecoveryRedo.ApplyAsync(store, Analysis(record));
        (await Read(store, page)).Should().Equal(current);
    }

    [Test]
    public async Task CorruptPageIsRepairedFromVerifiedImageButWrongTypeStopsRecovery()
    {
        await using var store = new InMemoryPageStore(); var page = await store.AllocateAsync(PageType.Heap);
        var before = await Read(store, page); var corrupt = before.ToArray(); corrupt[^1] ^= 1; await store.WriteAsync(page, corrupt);
        var record = new WalRecord(new LogSequenceNumber(10), default, new TransactionId(1), WalRecordType.PageChange,
            PhysicalPageChangeCodec.Write(new(page, PageType.Heap, before, Image(page, PageType.Heap, 10, 4))));
        await RecoveryRedo.ApplyAsync(store, Analysis(record));
        var wrong = record with { Payload = PhysicalPageChangeCodec.Write(new(page, PageType.Catalog, before,
            Image(page, PageType.Catalog, 10, 4))) };
        await ((Func<Task>)(async () => await RecoveryRedo.ApplyAsync(store, Analysis(wrong)))).Should().ThrowAsync<Exception>();
    }

    private static RecoveryAnalysis Analysis(WalRecord record) => new(
        new Dictionary<TransactionId, TransactionState> { [record.TransactionId] = TransactionState.Committed },
        new Dictionary<PageId, LogSequenceNumber>(), [record], 0, false);
    private static async Task<byte[]> Read(IPageStore store, PageId id) { var bytes = new byte[store.PageSize]; await store.ReadAsync(id, bytes); return bytes; }
    private static byte[] Image(PageId id, PageType type, ulong lsn, byte marker)
    { var bytes = new byte[PageConstants.DefaultSize]; PageHeaderCodec.Write(bytes, new(id, type, PageFormatVersion.Current,
        new LogSequenceNumber(lsn), PageChecksumAlgorithm.Crc32, 0)); bytes[^1] = marker; PageChecksum.WriteChecksum(bytes, bytes.Length); return bytes; }
}
