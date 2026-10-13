using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Exposures.Logging;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Logging;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class WalCodecBoundaryTests
{
    [TestCase(0u)]
    [TestCase(39u)]
    [TestCase((uint)(WalFormatConstants.RecordHeaderLength + WalFormatConstants.MaximumPayloadLength + 1))]
    [TestCase(0x80000000u)]
    [TestCase(uint.MaxValue)]
    public void InvalidUnsignedLengthsAreClassifiedBeforeConversionOrAllocation(uint length)
    {
        var prefix = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix,length);
        ((Func<WalReadResult>)(() => WalReadResultExposure.ReadWalReadResult(prefix))).Should().Throw<StorageCorruptionException>().Which.GetBaseException().Should().BeOfType<StorageCorruptionException>();
        var envelope = new byte[WalFormatConstants.RecordHeaderLength];
        prefix.CopyTo(envelope,0);
        ((Func<WalRecord>)(() => WalRecordExposure.ReadWalRecord(envelope))).Should().Throw<StorageFormatException>().Which.GetBaseException().Should().BeOfType<StorageFormatException>();
    }

    [Test]
    public void EveryPartialTailReportsTheCompletePrefixAndKeepsSourceBytesUnchanged()
    {
        var record = new WalRecord
        {
            Lsn = new LogSequenceNumber
            {
                Value = 1
            },
            TransactionId = new TransactionId
            {
                Value = 2
            },
            Type = WalRecordType.Begin,
            Payload = new byte[]
            {
                1,
                2,
                3,
                4,
                5
            }
        };
        var complete = WalRecordExposure.WriteWalRecord(record);
        for (var tailLength = 0; tailLength < complete.Length; tailLength++)
        {
            var bytes = complete.Concat(complete.Take(tailLength)).ToArray();
            var original = bytes.ToArray();
            var result = WalReadResultExposure.ReadWalReadResult(bytes);
            result.ValidLength.Should().Be(complete.Length);
            result.HasIncompleteTail.Should().Be(tailLength != 0);
            result.Records.Should().HaveCount(1);
            result.Records[0].Payload.ToArray().Should().Equal(1, 2, 3, 4, 5);
            bytes.Should().Equal(original);
            ((Action)(() => ((IList<WalRecord>)result.Records).Clear())).Should().Throw<NotSupportedException>();
        }

        var empty = WalReadResultExposure.ReadWalReadResult([]);
        empty.ValidLength.Should().Be(0);
        empty.HasIncompleteTail.Should().BeFalse();
        empty.Records.Should().BeEmpty();
    }

    [Test]
    public void MissingWriteModelsPreserveTheOriginalArgumentCause()
    {
        ((Func<byte[]>)(() => WalRecordExposure.WriteWalRecord(null!))).Should().Throw<StorageModelValidationException>().Which.GetBaseException().Should().BeOfType<ArgumentNullException>();
        ((Func<byte[]>)(() => WalSegmentHeaderExposure.WriteWalSegmentHeader(null!))).Should().Throw<StorageModelValidationException>().Which.GetBaseException().Should().BeOfType<ArgumentNullException>();
    }

    [Test]
    public void SegmentValidationPreservesReservedByteAndChecksumFailureOrdering()
    {
        var bytes = WalSegmentHeaderExposure.WriteWalSegmentHeader(new WalSegmentHeader { DatabaseId = new DatabaseId { Value = Guid.NewGuid() }, Timeline = 1, SegmentNumber = 2 });
        bytes[6] = 1;
        ((Func<WalSegmentHeader>)(() => WalSegmentHeaderExposure.ReadWalSegmentHeader(bytes))).Should().Throw<StorageFormatException>().Which.Message.Should().Contain("Reserved");
        bytes[6] = 0;
        bytes[24] ^= 1;
        ((Func<WalSegmentHeader>)(() => WalSegmentHeaderExposure.ReadWalSegmentHeader(bytes))).Should().Throw<StorageCorruptionException>().Which.Message.Should().Contain("checksum");
    }
}