// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Exposures.Logging;
using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Storage;
using sql_storage_engine.Transactions;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Exposures.Identifiers;

namespace sql_storage_engine.UnitTests;
public sealed class RecoveryAnalyzerTests
{
    private static readonly DatabaseId Database = new sql_storage_engine.Models.Identifiers.DatabaseId
    {
        Value = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")
    };
    private static WalRecord Record(ulong lsn, ulong tx, WalRecordType type, ulong? page = null)
    {
        var payload = page is null ? Array.Empty<byte>() : new byte[8];
        if (page is not null)
            BinaryPrimitives.WriteUInt64LittleEndian(payload, page.Value);
        return new sql_storage_engine.Models.Logging.WalRecord
        {
            Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = lsn
            },
            PreviousLsn = default,
            TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
            {
                Value = tx
            },
            Type = type,
            Payload = payload
        };
    }

    [Test]
    public void AnalyzeClassifiesCommittedRolledBackAndIncompleteTransactionsAndDirtyPages()
    {
        var bytes = new[]
        {
            Record(1,1,WalRecordType.PageChange,7),
            Record(2,1,WalRecordType.Commit),
            Record(3,2,WalRecordType.Rollback),
            Record(4,3,WalRecordType.Begin)
        }.SelectMany(WalRecordExposure.WriteWalRecord).ToArray();
        var result = sql_storage_engine.Exposures.Logging.RecoveryAnalysisExposure.Analyze(new WalSegmentHeader { DatabaseId = Database, Timeline = 1, SegmentNumber = 0 }, Database, 1, bytes);
        result.Transactions[new sql_storage_engine.Models.Identifiers.TransactionId { Value = 1 }].Should().Be(TransactionState.Committed);
        result.Transactions[new sql_storage_engine.Models.Identifiers.TransactionId { Value = 2 }].Should().Be(TransactionState.RolledBack);
        result.Transactions[new sql_storage_engine.Models.Identifiers.TransactionId { Value = 3 }].Should().Be(TransactionState.Active);
        result.DirtyPages[new sql_storage_engine.Models.Identifiers.PageId { Value = 7 }].Should().Be(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 1 });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void WrongDatabaseOrTimelineIsRejected(bool database)
    {
        var segment = database ? new WalSegmentHeader
        {
            DatabaseId = DatabaseIdExposure.CreateDatabaseId(),
            Timeline = 1,
            SegmentNumber = 0
        }

        : new WalSegmentHeader
        {
            DatabaseId = Database,
            Timeline = 2,
            SegmentNumber = 0
        };
        ((Func<RecoveryAnalysis>)(() => sql_storage_engine.Exposures.Logging.RecoveryAnalysisExposure.Analyze(segment,Database,1,[]))).Should().Throw<StorageCorruptionException>();
    }

    [Test]
    public void IncompleteFinalRecordIsIgnoredAtLastValidBoundary()
    {
        var complete = WalRecordExposure.WriteWalRecord(Record(1,1,WalRecordType.Begin));
        var bytes = complete.Concat(WalRecordExposure.WriteWalRecord(Record(2,1,WalRecordType.Commit))[..10]).ToArray();
        var result = sql_storage_engine.Exposures.Logging.RecoveryAnalysisExposure.Analyze(new WalSegmentHeader { DatabaseId = Database, Timeline = 1, SegmentNumber = 0 }, Database, 1, bytes);
        result.TruncatedTail.Should().BeTrue();
        result.ValidLength.Should().Be(complete.Length);
        result.Records.Should().HaveCount(1);
    }

    [Test]
    public void MidLogCorruptionFailsRecovery()
    {
        var first = WalRecordExposure.WriteWalRecord(Record(1,1,WalRecordType.Begin));
        var second = WalRecordExposure.WriteWalRecord(Record(2,1,WalRecordType.Commit));
        first[12] ^= 1;
        ((Func<RecoveryAnalysis>)(() => sql_storage_engine.Exposures.Logging.RecoveryAnalysisExposure.Analyze(new WalSegmentHeader { DatabaseId = Database, Timeline = 1, SegmentNumber = 0 }, Database, 1, first.Concat(second).ToArray()))).Should().Throw<StorageCorruptionException>();
    }

    [Test]
    public void CheckpointResumeSnapshotsCallerCollectionsAndPreservesFirstDirtyLsn()
    {
        var tx = new TransactionId { Value = 1 };
        var page = new PageId { Value = 7 };
        var dirty = new Dictionary<PageId, LogSequenceNumber> { [page] = new LogSequenceNumber { Value = 2 } };
        var active = new Dictionary<TransactionId, LogSequenceNumber> { [tx] = new LogSequenceNumber { Value = 1 } };
        var records = new List<WalRecord> { Record(3,1,WalRecordType.PageChange,7), Record(4,1,WalRecordType.Commit) };
        var result = RecoveryAnalysisExposure.ResumeFromCheckpoint(new CheckpointState { DirtyPages = dirty, ActiveTransactions = active }, records);
        dirty.Clear(); active.Clear(); records.Clear();
        result.Records.Should().HaveCount(2);
        result.DirtyPages[page].Value.Should().Be(2);
        result.Transactions[tx].Should().Be(TransactionState.Committed);
        ((Action)(() => ((IDictionary<PageId, LogSequenceNumber>)result.DirtyPages).Clear())).Should().Throw<NotSupportedException>();
        ((Action)(() => ((IList<WalRecord>)result.Records).Clear())).Should().Throw<NotSupportedException>();
    }

    [Test]
    public void NullCheckpointRecordRetainsValidationCause()
    {
        var checkpoint = new CheckpointState { ActiveTransactions = new Dictionary<TransactionId, LogSequenceNumber>(), DirtyPages = new Dictionary<PageId, LogSequenceNumber>() };
        ((Action)(() => RecoveryAnalysisExposure.ResumeFromCheckpoint(checkpoint,new WalRecord[] { null! })))
            .Should().Throw<ArgumentException>().Which.InnerException.Should().BeAssignableTo<ArgumentException>();
    }
}