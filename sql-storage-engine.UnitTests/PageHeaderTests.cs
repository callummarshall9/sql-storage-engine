using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class PageHeaderTests
{
    [TestCase(PageType.DatabaseHeader)]
    [TestCase(PageType.Catalog)]
    [TestCase(PageType.Heap)]
    [TestCase(PageType.BPlusTreeInternal)]
    [TestCase(PageType.BPlusTreeLeaf)]
    [TestCase(PageType.Overflow)]
    [TestCase(PageType.Free)]
    public void Codec_RoundTripsEverySupportedPageType(PageType type)
    {
        var expected = Header(type, ulong.MaxValue, ulong.MaxValue, uint.MaxValue);
        var bytes = new byte[PageHeaderCodec.EncodedLength];
        PageHeaderCodec.Write(bytes, expected);
        PageHeaderCodec.Read(bytes).Should().Be(expected);
    }

    [Test]
    public void Codec_WritesDocumentedGoldenBytes()
    {
        var bytes = new byte[PageHeaderCodec.EncodedLength];
        PageHeaderCodec.Write(bytes, Header(PageType.Heap, 0x0102030405060708, 0x1112131415161718, 0xaabbccdd));
        Convert.ToHexString(bytes).Should().Be(
            "08070605040302010300010018171615141312110100000000000000DDCCBBAA");
    }

    [Test]
    public void Codec_RejectsTruncationReservedBytesAndUnsupportedMetadata()
    {
        Action shortRead = () => PageHeaderCodec.Read(new byte[31]);
        shortRead.Should().Throw<StorageFormatException>();
        var bytes = new byte[32];
        PageHeaderCodec.Write(bytes, Header(PageType.Heap));
        bytes[22] = 1;
        ((Action)(() => PageHeaderCodec.Read(bytes))).Should().Throw<StorageFormatException>();
        ((Action)(() => Header((PageType)999).Validate(new PageId(1)))).Should().Throw<StorageFormatException>();
        ((Action)(() => (Header(PageType.Heap) with { FormatVersion = new PageFormatVersion(2) }).Validate(new PageId(1))))
            .Should().Throw<StorageFormatException>();
    }

    [Test]
    public void Checksum_DetectsMutationAndCanBeRewritten()
    {
        var page = new byte[PageConstants.DefaultSize];
        PageHeaderCodec.Write(page, Header(PageType.Heap));
        page[^1] = 42;
        PageChecksum.WriteChecksum(page, page.Length);
        ((Action)(() => PageChecksum.ValidateChecksum(page, page.Length))).Should().NotThrow();
        page[100] ^= 1;
        ((Action)(() => PageChecksum.ValidateChecksum(page, page.Length))).Should().Throw<StorageCorruptionException>();
        PageChecksum.WriteChecksum(page, page.Length);
        ((Action)(() => PageChecksum.ValidateChecksum(page, page.Length))).Should().NotThrow();
    }

    private static PageHeader Header(PageType type, ulong pageId = 1, ulong lsn = 0, uint checksum = 0) =>
        new(new PageId(pageId), type, PageFormatVersion.Current, new LogSequenceNumber(lsn), PageChecksumAlgorithm.Crc32, checksum);
}
