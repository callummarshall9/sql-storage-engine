using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Storage;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;

public sealed class RecoveryAnalyzerTests
{
    private static readonly DatabaseId Database = new(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));
    private static WalRecord Record(ulong lsn, ulong tx, WalRecordType type, ulong? page = null)
    { var payload = page is null ? Array.Empty<byte>() : new byte[8]; if (page is not null) BinaryPrimitives.WriteUInt64LittleEndian(payload, page.Value); return new(new LogSequenceNumber(lsn), default, new TransactionId(tx), type, payload); }

    [Test]
    public void AnalyzeClassifiesCommittedRolledBackAndIncompleteTransactionsAndDirtyPages()
    {
        var bytes = new[] { Record(1, 1, WalRecordType.PageChange, 7), Record(2, 1, WalRecordType.Commit),
            Record(3, 2, WalRecordType.Rollback), Record(4, 3, WalRecordType.Begin) }
            .SelectMany(WalFormat.WriteRecord).ToArray();
        var result = RecoveryAnalyzer.Analyze(new WalSegmentHeader(Database, 1, 0), Database, 1, bytes);
        result.Transactions[new TransactionId(1)].Should().Be(TransactionState.Committed);
        result.Transactions[new TransactionId(2)].Should().Be(TransactionState.RolledBack);
        result.Transactions[new TransactionId(3)].Should().Be(TransactionState.Active);
        result.DirtyPages[new PageId(7)].Should().Be(new LogSequenceNumber(1));
    }

    [TestCase(true)] [TestCase(false)]
    public void WrongDatabaseOrTimelineIsRejected(bool database)
    {
        var segment = database ? new WalSegmentHeader(DatabaseId.New(), 1, 0) : new WalSegmentHeader(Database, 2, 0);
        ((Func<RecoveryAnalysis>)(() => RecoveryAnalyzer.Analyze(segment, Database, 1, []))).Should().Throw<StorageCorruptionException>();
    }

    [Test]
    public void IncompleteFinalRecordIsIgnoredAtLastValidBoundary()
    {
        var complete = WalFormat.WriteRecord(Record(1, 1, WalRecordType.Begin));
        var bytes = complete.Concat(WalFormat.WriteRecord(Record(2, 1, WalRecordType.Commit))[..10]).ToArray();
        var result = RecoveryAnalyzer.Analyze(new WalSegmentHeader(Database, 1, 0), Database, 1, bytes);
        result.TruncatedTail.Should().BeTrue(); result.ValidLength.Should().Be(complete.Length); result.Records.Should().HaveCount(1);
    }

    [Test]
    public void MidLogCorruptionFailsRecovery()
    {
        var first = WalFormat.WriteRecord(Record(1, 1, WalRecordType.Begin));
        var second = WalFormat.WriteRecord(Record(2, 1, WalRecordType.Commit));
        first[12] ^= 1;
        ((Func<RecoveryAnalysis>)(() => RecoveryAnalyzer.Analyze(new WalSegmentHeader(Database, 1, 0), Database, 1,
            first.Concat(second).ToArray()))).Should().Throw<StorageCorruptionException>();
    }
}
