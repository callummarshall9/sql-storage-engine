// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Exposures.Logging;
using AwesomeAssertions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.UnitTests;
public sealed class CheckpointTests
{
    private static CheckpointState State() => new sql_storage_engine.Models.Logging.CheckpointState
    {
        SafeRecoveryLsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
        {
            Value = 50
        },
        ActiveTransactions = new Dictionary<TransactionId, LogSequenceNumber>
        {
            [new sql_storage_engine.Models.Identifiers.TransactionId { Value = 2 }] = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = 20
            }
        },
        DirtyPages = new Dictionary<PageId, LogSequenceNumber>
        {
            [new sql_storage_engine.Models.Identifiers.PageId { Value = 7 }] = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = 30
            }
        }
    };
    [Test]
    public void CheckpointStateRoundTripsWithDeterministicOrdering()
    {
        var bytes = CheckpointStateExposure.WriteCheckpointState(State());
        CheckpointStateExposure.ReadCheckpointState(bytes).Should().BeEquivalentTo(State());
        CheckpointStateExposure.WriteCheckpointState(State()).Should().Equal(bytes);
    }

    [Test]
    public async Task DurableCheckpointPublishesOnlyAfterFlushAndInterruptedPublicationKeepsPrevious()
    {
        var device = new WriteAheadLogTests.MemoryWalDevice();
        var wal = await sql_storage_engine.Exposures.Logging.WriteAheadLogExposure.OpenAsync(device);
        sql_storage_engine.Exposures.Logging.CheckpointReferenceExposure reference = new sql_storage_engine.Exposures.Logging.CheckpointReferenceExposure();

        var first = await wal.CreateCheckpointLogSequenceNumberAsync(State(),reference);
        reference.ReadLatestCheckpointLsn().Should().Be(first);
        wal.ReadDurableLsn().Should().Be(first);
        reference.SetPublicationFailure(failPublish: true);
        await ((Func<Task>)(async () => await wal.CreateCheckpointLogSequenceNumberAsync(State(),reference))).Should().ThrowAsync<IOException>();
        reference.ReadLatestCheckpointLsn().Should().Be(first);
    }

    [Test]
    public void ActiveTransactionWalPreventsUnsafeRetention()
    {
        CheckpointStateExposure.ReadCheckpointStateRetention(State()).Should().Be(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 20 });
        CheckpointStateExposure.ReadCheckpointStateRetention(State()with { ActiveTransactions = new Dictionary<TransactionId, LogSequenceNumber>() }).Should().Be(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 50 });
    }

    [Test]
    public void RecoveryFromCheckpointProducesSameStateAsFullLogAnalysis()
    {
        var database = new sql_storage_engine.Models.Identifiers.DatabaseId
        {
            Value = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")
        };
        var prefix = new WalRecord
        {
            Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = 20
            },
            PreviousLsn = default,
            TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
            {
                Value = 2
            },
            Type = WalRecordType.Begin,
            Payload = Array.Empty<byte>()
        };
        var pagePayload = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(pagePayload,7);
        var dirty = new WalRecord
        {
            Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = 30
            },
            PreviousLsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = 20
            },
            TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
            {
                Value = 2
            },
            Type = WalRecordType.PageChange,
            Payload = pagePayload
        };
        var suffix = new WalRecord
        {
            Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = 60
            },
            PreviousLsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = 30
            },
            TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
            {
                Value = 2
            },
            Type = WalRecordType.Commit,
            Payload = Array.Empty<byte>()
        };
        var full = sql_storage_engine.Exposures.Logging.RecoveryAnalysisExposure.Analyze(new WalSegmentHeader { DatabaseId = database, Timeline = 1, SegmentNumber = 0 }, database, 1, new[] { prefix, dirty, suffix }.SelectMany(WalRecordExposure.WriteWalRecord).ToArray());
        var checkpoint = new CheckpointState
        {
            SafeRecoveryLsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = 20
            },
            ActiveTransactions = new Dictionary<TransactionId, LogSequenceNumber>
            {
                [new sql_storage_engine.Models.Identifiers.TransactionId { Value = 2 }] = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
                {
                    Value = 30
                }
            },
            DirtyPages = new Dictionary<PageId, LogSequenceNumber>
            {
                [new sql_storage_engine.Models.Identifiers.PageId { Value = 7 }] = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
                {
                    Value = 30
                }
            }
        };
        var resumed = sql_storage_engine.Exposures.Logging.RecoveryAnalysisExposure.ResumeFromCheckpoint(checkpoint,[suffix]);
        resumed.Transactions.Should().BeEquivalentTo(full.Transactions);
        resumed.DirtyPages.Should().BeEquivalentTo(full.DirtyPages);
    }

    [Test]
    public void UnknownVersionAndEveryTruncationAreRejected()
    {
        var bytes = CheckpointStateExposure.WriteCheckpointState(State());
        var unknown = bytes.ToArray();
        unknown[0] = 2;
        ((Func<CheckpointState>)(() => CheckpointStateExposure.ReadCheckpointState(unknown))).Should().Throw<sql_storage_engine.Storage.StorageFormatException>();
        for (var length = 0; length < bytes.Length; length++)
            ((Func<CheckpointState>)(() => CheckpointStateExposure.ReadCheckpointState(bytes.AsSpan(0, length)))).Should().Throw<sql_storage_engine.Storage.StorageFormatException>();
    }
}