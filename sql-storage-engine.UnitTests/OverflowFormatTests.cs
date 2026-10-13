// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Exposures.Overflow;
using AwesomeAssertions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Overflow;

namespace sql_storage_engine.UnitTests;
public sealed class OverflowFormatTests
{
    [Test]
    public void ReferenceCodec_RoundTripsAndHasGoldenLittleEndianBytes()
    {
        var expected = new OverflowReference
        {
            FirstPageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = 0x0102030405060708
            },
            TotalLength = 0x00123456
        };
        var bytes = new byte[OverflowReferenceFormat.EncodedLength];
        OverflowReferenceExposure.WriteOverflowReference(bytes,expected);
        Convert.ToHexString(bytes).Should().Be("08070605040302015634120000000000");
        OverflowReferenceExposure.ReadOverflowReference(bytes).Should().Be(expected);
    }

    [Test]
    public void PageCodec_RoundTripsHeaderPayloadAndGoldenFields()
    {
        var page = new byte[sql_storage_engine.Models.Pages.PageConstants.DefaultSize];
        sql_storage_engine.Exposures.Overflow.OverflowPageHeaderExposure.WriteOverflowPageHeader(page,(new sql_storage_engine.Models.Identifiers.PageId { Value = 5 }).Value,(new sql_storage_engine.Models.Identifiers.PageId { Value = 8 }).Value,new byte[] { 1, 2, 3 });
        sql_storage_engine.Exposures.Overflow.OverflowPageHeaderExposure.ReadOverflowPageHeader(page,(new sql_storage_engine.Models.Identifiers.PageId { Value = 5 }).Value).Should().Be(new OverflowPageHeader { NextPageId = new sql_storage_engine.Models.Identifiers.PageId { Value = 8 }, UsedLength = 3 });
        sql_storage_engine.Exposures.Overflow.OverflowPageHeaderExposure.ReadOverflowPageHeaderPayload(page,(new sql_storage_engine.Models.Identifiers.PageId { Value = 5 }).Value).ToArray().Should().Equal(1, 2, 3);
        Convert.ToHexString(page.AsSpan(32, 16)).Should().Be("01080000000000000003000000000000");
    }

    [Test]
    public void Codecs_RejectInvalidLengthsTypesAndNextIds()
    {
        ((Action)(() => OverflowReferenceExposure.CheckOverflowReference(new OverflowReference { FirstPageId = new sql_storage_engine.Models.Identifiers.PageId { Value = 0 }, TotalLength = 1 }))).Should().Throw<StorageFormatException>();
        ((Action)(() => OverflowReferenceExposure.CheckOverflowReference(new OverflowReference { FirstPageId = new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, TotalLength = 0 }))).Should().Throw<StorageFormatException>();
        ((Action)(() => OverflowReferenceExposure.CheckOverflowReference(new OverflowReference { FirstPageId = new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, TotalLength = OverflowReferenceFormat.MaximumValueLength + 1 }))).Should().Throw<StorageFormatException>();
        var page = new byte[sql_storage_engine.Models.Pages.PageConstants.DefaultSize];
        ((Action)(() => sql_storage_engine.Exposures.Overflow.OverflowPageHeaderExposure.WriteOverflowPageHeader(page,(new sql_storage_engine.Models.Identifiers.PageId { Value = 2 }).Value,(new sql_storage_engine.Models.Identifiers.PageId { Value = 2 }).Value,new byte[] { 1 }))).Should().Throw<ArgumentException>();
        sql_storage_engine.Exposures.Heap.HeapPageLayoutExposure.InitializeHeapPageLayout(page,(new sql_storage_engine.Models.Identifiers.PageId { Value = 2 }).Value);
        ((Action)(() => sql_storage_engine.Exposures.Overflow.OverflowPageHeaderExposure.ReadOverflowPageHeader(page,(new sql_storage_engine.Models.Identifiers.PageId { Value = 2 }).Value))).Should().Throw<StorageFormatException>();
    }
}