// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Models.Pages;

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
        var expected = Header(type,ulong.MaxValue,ulong.MaxValue,uint.MaxValue);
        var bytes = new byte[sql_storage_engine.Models.Pages.PageHeaderFormat.EncodedLength];
        sql_storage_engine.Exposures.Pages.PageHeaderExposure.WritePageHeader(bytes,expected);
        sql_storage_engine.Exposures.Pages.PageHeaderExposure.ReadPageHeader(bytes).Should().Be(expected);
    }

    [Test]
    public void Codec_WritesDocumentedGoldenBytes()
    {
        var bytes = new byte[sql_storage_engine.Models.Pages.PageHeaderFormat.EncodedLength];
        sql_storage_engine.Exposures.Pages.PageHeaderExposure.WritePageHeader(bytes,Header(PageType.Heap,0x0102030405060708,0x1112131415161718,0xaabbccdd));
        Convert.ToHexString(bytes).Should().Be("08070605040302010300010018171615141312110100000000000000DDCCBBAA");
    }

    [Test]
    public void Codec_RejectsTruncationReservedBytesAndUnsupportedMetadata()
    {
        Action shortRead = () => sql_storage_engine.Exposures.Pages.PageHeaderExposure.ReadPageHeader(new byte[31]);
        shortRead.Should().Throw<StorageFormatException>();
        var bytes = new byte[32];
        sql_storage_engine.Exposures.Pages.PageHeaderExposure.WritePageHeader(bytes,Header(PageType.Heap));
        bytes[22] = 1;
        ((Action)(() => sql_storage_engine.Exposures.Pages.PageHeaderExposure.ReadPageHeader(bytes))).Should().Throw<StorageFormatException>();
        ((Action)(() => sql_storage_engine.Exposures.Pages.PageHeaderExposure.CheckPageHeader(pageHeader: Header((PageType)999),(new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }).Value))).Should().Throw<StorageFormatException>();
        ((Action)(() => sql_storage_engine.Exposures.Pages.PageHeaderExposure.CheckPageHeader(pageHeader: (Header(PageType.Heap)with { FormatVersion = new PageFormatVersion { Value = 2 } }),(new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }).Value))).Should().Throw<StorageFormatException>();
    }

    [Test]
    public void Checksum_DetectsMutationAndCanBeRewritten()
    {
        var page = new byte[sql_storage_engine.Models.Pages.PageConstants.DefaultSize];
        sql_storage_engine.Exposures.Pages.PageHeaderExposure.WritePageHeader(page,Header(PageType.Heap));
        page[^1] = 42;
        PageChecksumExposure.WriteChecksum(page,page.Length);
        ((Action)(() => PageChecksumExposure.ValidateChecksum(page,page.Length))).Should().NotThrow();
        page[100] ^= 1;
        ((Action)(() => PageChecksumExposure.ValidateChecksum(page,page.Length))).Should().Throw<StorageCorruptionException>();
        PageChecksumExposure.WriteChecksum(page,page.Length);
        ((Action)(() => PageChecksumExposure.ValidateChecksum(page,page.Length))).Should().NotThrow();
    }

    private static PageHeader Header(PageType type, ulong pageId = 1, ulong lsn = 0, uint checksum = 0) => new sql_storage_engine.Models.Pages.PageHeader
    {
        PageId = new sql_storage_engine.Models.Identifiers.PageId
        {
            Value = pageId
        },
        PageType = type,
        FormatVersion = new sql_storage_engine.Models.Pages.PageFormatVersion
        {
            Value = sql_storage_engine.Models.Pages.PageHeaderFormat.CurrentVersion
        },
        PageLogSequenceNumber = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
        {
            Value = lsn
        },
        ChecksumAlgorithm = PageChecksumAlgorithm.Crc32,
        Checksum = checksum
    };
}