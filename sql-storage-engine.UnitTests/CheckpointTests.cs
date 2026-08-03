using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;

namespace sql_storage_engine.UnitTests;

public sealed class CheckpointTests
{
    private static CheckpointState State() => new(new LogSequenceNumber(50),
        new Dictionary<TransactionId, LogSequenceNumber> { [new TransactionId(2)] = new(20) },
        new Dictionary<PageId, LogSequenceNumber> { [new PageId(7)] = new(30) });

    [Test]
    public void CheckpointStateRoundTripsWithDeterministicOrdering()
    {
        var bytes = CheckpointCodec.Write(State());
        CheckpointCodec.Read(bytes).Should().BeEquivalentTo(State());
        CheckpointCodec.Write(State()).Should().Equal(bytes);
    }

    [Test]
    public async Task DurableCheckpointPublishesOnlyAfterFlushAndInterruptedPublicationKeepsPrevious()
    {
        var device = new WriteAheadLogTests.MemoryWalDevice(); var wal = await WriteAheadLog.OpenAsync(device);
        var reference = new MemoryCheckpointReference(); var manager = new CheckpointManager(wal, reference);
        var first = await manager.CreateAsync(State());
        reference.LatestCheckpointLsn.Should().Be(first); wal.DurableLsn.Should().Be(first);
        reference.FailPublish = true;
        await ((Func<Task>)(async () => await manager.CreateAsync(State()))).Should().ThrowAsync<IOException>();
        reference.LatestCheckpointLsn.Should().Be(first);
    }

    [Test]
    public void ActiveTransactionWalPreventsUnsafeRetention()
    {
        CheckpointManager.GetRetentionLsn(State()).Should().Be(new LogSequenceNumber(20));
        CheckpointManager.GetRetentionLsn(State() with { ActiveTransactions = new Dictionary<TransactionId, LogSequenceNumber>() })
            .Should().Be(new LogSequenceNumber(50));
    }

    [Test]
    public void RecoveryFromCheckpointProducesSameStateAsFullLogAnalysis()
    {
        var database = new DatabaseId(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));
        var prefix = new WalRecord(new LogSequenceNumber(20), default, new TransactionId(2), WalRecordType.Begin, Array.Empty<byte>());
        var pagePayload = new byte[8]; System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(pagePayload, 7);
        var dirty = new WalRecord(new LogSequenceNumber(30), new LogSequenceNumber(20), new TransactionId(2), WalRecordType.PageChange, pagePayload);
        var suffix = new WalRecord(new LogSequenceNumber(60), new LogSequenceNumber(30), new TransactionId(2), WalRecordType.Commit, Array.Empty<byte>());
        var full = RecoveryAnalyzer.Analyze(new WalSegmentHeader(database, 1, 0), database, 1,
            new[] { prefix, dirty, suffix }.SelectMany(WalFormat.WriteRecord).ToArray());
        var checkpoint = new CheckpointState(new LogSequenceNumber(20),
            new Dictionary<TransactionId, LogSequenceNumber> { [new TransactionId(2)] = new(30) },
            new Dictionary<PageId, LogSequenceNumber> { [new PageId(7)] = new(30) });
        var resumed = RecoveryAnalyzer.ResumeFromCheckpoint(checkpoint, [suffix]);
        resumed.Transactions.Should().BeEquivalentTo(full.Transactions);
        resumed.DirtyPages.Should().BeEquivalentTo(full.DirtyPages);
    }

    [Test]
    public void UnknownVersionAndEveryTruncationAreRejected()
    {
        var bytes = CheckpointCodec.Write(State());
        var unknown = bytes.ToArray(); unknown[0] = 2;
        ((Func<CheckpointState>)(() => CheckpointCodec.Read(unknown))).Should().Throw<sql_storage_engine.Storage.StorageFormatException>();
        for (var length = 0; length < bytes.Length; length++)
            ((Func<CheckpointState>)(() => CheckpointCodec.Read(bytes.AsSpan(0, length)))).Should()
                .Throw<sql_storage_engine.Storage.StorageFormatException>();
    }
}
