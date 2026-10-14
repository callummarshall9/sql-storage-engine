// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Brokers.Checksums;
using sql_storage_engine.Models.Checksums;
using System.Buffers.Binary;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.PageHeaders;
internal sealed partial class PageHeaderService : IPageHeaderService
{
    public void WritePageHeader(Span<byte> destination, PageHeader pageHeader) =>
        TryCatch(operation: static context =>
    {
        ValidatePageHeaderOnWrite(destination: context.destination, pageHeader: context.pageHeader);
        context.destination[..PageHeaderFormat.EncodedLength].Clear();
        BinaryPrimitives.WriteUInt64LittleEndian(destination: context.destination, value: context.pageHeader.PageId.Value);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: context.destination[8..], value: (ushort)context.pageHeader.PageType);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: context.destination[10..], value: context.pageHeader.FormatVersion.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: context.destination[12..], value: context.pageHeader.PageLogSequenceNumber.Value);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: context.destination[20..], value: (ushort)context.pageHeader.ChecksumAlgorithm);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: context.destination[PageHeaderFormat.ChecksumOffset..], value: context.pageHeader.Checksum);
    }, context: new WritablePageHeaderContext { destination = destination, pageHeader = pageHeader });

    public PageHeader ReadPageHeader(ReadOnlySpan<byte> source) =>
        TryCatch(operation: static context =>
    {
        ValidatePageHeaderSource(source: context.source);

        var pageHeader = new PageHeader
        {
            PageId = new PageId
            {
                Value = BinaryPrimitives.ReadUInt64LittleEndian(source: context.source)
            },
            PageType = (PageType)BinaryPrimitives.ReadUInt16LittleEndian(source: context.source[8..]),
            FormatVersion = new PageFormatVersion
            {
                Value = BinaryPrimitives.ReadUInt16LittleEndian(source: context.source[10..])
            },
            PageLogSequenceNumber = new LogSequenceNumber
            {
                Value = BinaryPrimitives.ReadUInt64LittleEndian(source: context.source[12..])
            },
            ChecksumAlgorithm = (PageChecksumAlgorithm)BinaryPrimitives.ReadUInt16LittleEndian(source: context.source[20..]),
            Checksum = BinaryPrimitives.ReadUInt32LittleEndian(source: context.source[PageHeaderFormat.ChecksumOffset..])
        };

        ValidatePageHeader(pageHeader: pageHeader, expectedPageId: pageHeader.PageId.Value, expectedType: null);
        return pageHeader;
    }, context: new ReadablePageHeaderContext { source = source });

    public void CheckPageHeader(PageHeader pageHeader, ulong expectedPageId, PageType? expectedType = null) =>
        TryCatch(operation: () =>
    {
        ValidatePageHeader(pageHeader: pageHeader, expectedPageId: expectedPageId, expectedType: expectedType);
    });
    private static readonly ICrc32Broker crc32 = new Crc32Broker();
    public void WriteChecksum(Span<byte> page, int pageSize) =>
        TryCatch(operation: static context =>
    {
        ValidatePageSize(page: context.page, pageSize: context.pageSize);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: context.page[sql_storage_engine.Models.Pages.PageHeaderFormat.ChecksumOffset..], value: 0);
        var checksum = crc32.CalculateChecksum(bytes: context.page);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: context.page[sql_storage_engine.Models.Pages.PageHeaderFormat.ChecksumOffset..], value: checksum);
    }, context: new WritablePageChecksumContext { page = page, pageSize = pageSize });

    public void ValidateChecksum(ReadOnlySpan<byte> page, int pageSize) =>
        TryCatch(operation: static context =>
    {
        ValidatePageSize(page: context.page, pageSize: context.pageSize);
        var expected = BinaryPrimitives.ReadUInt32LittleEndian(source: context.page[sql_storage_engine.Models.Pages.PageHeaderFormat.ChecksumOffset..]);
        var actual = crc32.CalculateChecksumWithZeroedField(bytes: context.page, zeroOffset: sql_storage_engine.Models.Pages.PageHeaderFormat.ChecksumOffset);

        if (actual != expected)
        {
            throw new StorageCorruptionException(message: $"Page checksum mismatch: expected 0x{expected:x8}, calculated 0x{actual:x8}.");
        }
    }, context: new ReadablePageChecksumContext { page = page, pageSize = pageSize });
}