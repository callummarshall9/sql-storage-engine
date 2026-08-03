using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Overflow;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class OverflowFormatTests
{
    [Test]
    public void ReferenceCodec_RoundTripsAndHasGoldenLittleEndianBytes()
    {
        var expected = new OverflowReference(new PageId(0x0102030405060708), 0x00123456);
        var bytes = new byte[OverflowReferenceCodec.EncodedLength];
        OverflowReferenceCodec.Write(bytes, expected);
        Convert.ToHexString(bytes).Should().Be("08070605040302015634120000000000");
        OverflowReferenceCodec.Read(bytes).Should().Be(expected);
    }

    [Test]
    public void PageCodec_RoundTripsHeaderPayloadAndGoldenFields()
    {
        var page = new byte[PageConstants.DefaultSize];
        OverflowPageCodec.Initialize(page, new PageId(5), new PageId(8), new byte[] { 1, 2, 3 });
        OverflowPageCodec.ReadHeader(page, new PageId(5)).Should().Be(new OverflowPageHeader(new PageId(8), 3));
        OverflowPageCodec.ReadPayload(page, new PageId(5)).ToArray().Should().Equal(1, 2, 3);
        Convert.ToHexString(page.AsSpan(32, 16)).Should().Be("01080000000000000003000000000000");
    }

    [Test]
    public void Codecs_RejectInvalidLengthsTypesAndNextIds()
    {
        ((Action)(() => OverflowReferenceCodec.Validate(new OverflowReference(new PageId(0), 1))))
            .Should().Throw<StorageFormatException>();
        ((Action)(() => OverflowReferenceCodec.Validate(new OverflowReference(new PageId(1), 0))))
            .Should().Throw<StorageFormatException>();
        ((Action)(() => OverflowReferenceCodec.Validate(
            new OverflowReference(new PageId(1), OverflowReferenceCodec.MaximumValueLength + 1))))
            .Should().Throw<StorageFormatException>();
        var page = new byte[PageConstants.DefaultSize];
        ((Action)(() => OverflowPageCodec.Initialize(page, new PageId(2), new PageId(2), new byte[] { 1 })))
            .Should().Throw<ArgumentException>();
        Heap.HeapPageLayout.Initialize(page, new PageId(2));
        ((Action)(() => OverflowPageCodec.ReadHeader(page, new PageId(2)))).Should().Throw<StorageFormatException>();
    }
}
