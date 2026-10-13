using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Exposures.Overflow;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class OverflowReferenceBoundaryTests
{
    [TestCase(long.MinValue)]
    [TestCase(0L)]
    [TestCase((long)int.MaxValue + 1)]
    [TestCase(long.MaxValue)]
    public void InvalidSignedLengthsAreRejectedWithTheFormatCause(long length)
    {
        var bytes = new byte[OverflowReferenceFormat.EncodedLength];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes,1);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(8), length);
        ((Func<OverflowReference>)(() => OverflowReferenceExposure.ReadOverflowReference(bytes))).Should().Throw<StorageFormatException>().Which.GetBaseException().Should().BeOfType<StorageFormatException>();
    }

    [Test]
    public void MaximumReferenceKeepsItsGoldenBytesAndLeavesDestinationSuffixUntouched()
    {
        var reference = new OverflowReference
        {
            FirstPageId = new PageId
            {
                Value = ulong.MaxValue
            },
            TotalLength = int.MaxValue
        };
        var bytes = Enumerable.Repeat((byte)0xCC, OverflowReferenceFormat.EncodedLength + 4).ToArray();
        OverflowReferenceExposure.WriteOverflowReference(bytes, reference);
        Convert.ToHexString(bytes.AsSpan(0, 16)).Should().Be("FFFFFFFFFFFFFFFFFFFFFF7F00000000");
        bytes.AsSpan(16).ToArray().Should().OnlyContain(value => value == 0xCC);
        OverflowReferenceExposure.ReadOverflowReference(bytes).Should().Be(reference);
    }

    [Test]
    public void InvalidWriteIsRejectedBeforeChangingDestination()
    {
        var bytes = Enumerable.Repeat((byte)0xCC, OverflowReferenceFormat.EncodedLength).ToArray();
        ((Action)(() => OverflowReferenceExposure.WriteOverflowReference(bytes, default))).Should().Throw<StorageFormatException>();
        bytes.Should().OnlyContain(value => value == 0xCC);
        ((Action)(() => OverflowReferenceExposure.WriteOverflowReference(bytes.AsSpan(0, 8), new OverflowReference { FirstPageId = new PageId { Value = 1 }, TotalLength = 1 }))).Should().Throw<StorageModelValidationException>().Which.GetBaseException().Should().BeOfType<ArgumentException>();
        bytes.Should().OnlyContain(value => value == 0xCC);
    }

    [Test]
    public void EveryTruncatedReferenceIsRejectedBeforeReadingFields()
    {
        for (var length = 0; length < OverflowReferenceFormat.EncodedLength; length++)
        {
            var source = new byte[length];
            ((Func<OverflowReference>)(() => OverflowReferenceExposure.ReadOverflowReference(source))).Should().Throw<StorageFormatException>();
        }
    }
}