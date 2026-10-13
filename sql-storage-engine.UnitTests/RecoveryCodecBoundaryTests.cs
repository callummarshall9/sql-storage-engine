using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Exposures.Logging;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class RecoveryCodecBoundaryTests
{
    [TestCase(65_536u)]
    [TestCase(0x80000000u)]
    [TestCase(uint.MaxValue)]
    public void UnsignedCountsAndLengthsAreRejectedBeforeConversionOrAllocation(uint value)
    {
        foreach (var offset in new[]
        {
            16,
            20
        }

        )
        {
            var checkpoint = new byte[CheckpointFormat.HeaderLength];
            BinaryPrimitives.WriteUInt16LittleEndian(checkpoint,CheckpointFormat.Version);
            BinaryPrimitives.WriteUInt32LittleEndian(checkpoint.AsSpan(offset), value);
            ((Func<CheckpointState>)(() => CheckpointStateExposure.ReadCheckpointState(checkpoint))).Should().Throw<StorageFormatException>().Which.GetBaseException().Should().BeOfType<StorageFormatException>();
        }

        var physical = new byte[PhysicalPageChangeFormat.HeaderLength];
        BinaryPrimitives.WriteUInt32LittleEndian(physical.AsSpan(12), value);
        BinaryPrimitives.WriteUInt32LittleEndian(physical.AsSpan(16), value);
        ((Func<PhysicalPageChange>)(() => PhysicalPageChangeExposure.ReadPhysicalPageChange(physical))).Should().Throw<StorageFormatException>().Which.GetBaseException().Should().BeOfType<StorageFormatException>();
    }

    [Test]
    public void CheckpointOutputIsSortedAndDecodedMapsCannotBeMutated()
    {
        var state = new CheckpointState
        {
            SafeRecoveryLsn = new LogSequenceNumber
            {
                Value = 7
            },
            ActiveTransactions = new Dictionary<TransactionId, LogSequenceNumber>
            {
                [new TransactionId { Value = 2 }] = new LogSequenceNumber
                {
                    Value = 22
                },
                [new TransactionId { Value = 1 }] = new LogSequenceNumber
                {
                    Value = 11
                }
            },
            DirtyPages = new Dictionary<PageId, LogSequenceNumber>
            {
                [new PageId { Value = 5 }] = new LogSequenceNumber
                {
                    Value = 2
                }
            }
        };
        var bytes = CheckpointStateExposure.WriteCheckpointState(state);
        bytes.Length.Should().Be(72);
        BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(24)).Should().Be(1);
        BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(40)).Should().Be(2);
        var decoded = CheckpointStateExposure.ReadCheckpointState(bytes);
        CheckpointStateExposure.WriteCheckpointState(decoded).Should().Equal(bytes);
        ((Action)(() => ((IDictionary<TransactionId, LogSequenceNumber>)decoded.ActiveTransactions).Clear())).Should().Throw<NotSupportedException>();
        ((Action)(() => ((IDictionary<PageId, LogSequenceNumber>)decoded.DirtyPages).Clear())).Should().Throw<NotSupportedException>();
    }

    [Test]
    public void CheckpointWriteRejectsCountsThatCannotBeReadBack()
    {
        var transactions = Enumerable.Range(0, CheckpointFormat.MaximumEntries + 1).ToDictionary(index => new TransactionId { Value = (ulong)index }, _ => default(LogSequenceNumber));
        var state = new CheckpointState
        {
            ActiveTransactions = transactions,
            DirtyPages = new Dictionary<PageId, LogSequenceNumber>()
        };
        ((Func<byte[]>)(() => CheckpointStateExposure.WriteCheckpointState(state))).Should().Throw<StorageModelValidationException>().Which.GetBaseException().Should().BeOfType<ArgumentOutOfRangeException>();
    }

    [Test]
    public void PhysicalImagesRoundTripWithIndependentBuffersAndZeroReservedBytes()
    {
        var before = Enumerable.Repeat((byte)1, sql_storage_engine.Models.Pages.PageConstants.DefaultSize).ToArray();
        var after = Enumerable.Repeat((byte)2, sql_storage_engine.Models.Pages.PageConstants.DefaultSize).ToArray();
        var change = new PhysicalPageChange
        {
            PageId = new PageId
            {
                Value = 42
            },
            PageType = PageType.Heap,
            BeforeImage = before,
            AfterImage = after
        };
        var bytes = PhysicalPageChangeExposure.WritePhysicalPageChange(change);
        bytes.AsSpan(10, 2).ToArray().Should().OnlyContain(value => value == 0);
        bytes.AsSpan(20, 4).ToArray().Should().OnlyContain(value => value == 0);
        var decoded = PhysicalPageChangeExposure.ReadPhysicalPageChange(bytes);
        decoded.PageId.Should().Be(change.PageId);
        decoded.BeforeImage.ToArray().Should().Equal(before);
        decoded.AfterImage.ToArray().Should().Equal(after);
        bytes[^1] = 9;
        decoded.AfterImage.Span[^1].Should().Be(2);
        var invalid = change with
        {
            PageType = PageType.Unknown
        };
        ((Func<byte[]>)(() => PhysicalPageChangeExposure.WritePhysicalPageChange(invalid))).Should().Throw<StorageModelValidationException>();
    }
}