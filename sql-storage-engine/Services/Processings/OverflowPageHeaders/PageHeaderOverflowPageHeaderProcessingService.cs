// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Services.Foundations.PageHeaders;
using System.Buffers.Binary;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using System;
using sql_storage_engine.Models.Overflow;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.OverflowPageHeaders;
/// <summary>Encodes and validates one complete overflow page.</summary>
internal sealed partial class PageHeaderOverflowPageHeaderProcessingService : IPageHeaderOverflowPageHeaderProcessingService
{
    private static readonly IPageHeaderService pageHeaders = new PageHeaderService();
    public int CalculateOverflowPageHeaderCapacity(int pageSize) =>
        TryCatch(operation: () =>
    {
        ValidateOverflowPageHeaderSize(pageSize: pageSize);
        return GetPayloadCapacityCore(pageSize: pageSize);
    });

    public void WriteOverflowPageHeader(Span<byte> page, ulong pageId, ulong? nextPageId, ReadOnlySpan<byte> payload) =>
        TryCatch(operation: static context =>
    {
        ValidateOverflowPageHeaderOnWrite(page: context.page, pageId: context.pageId, nextPageId: context.nextPageId, payload: context.payload);
        InitializeCore(page: context.page, pageId: new PageId { Value = context.pageId }, nextPageId: context.nextPageId is null ? null : new PageId { Value = context.nextPageId.Value }, payload: context.payload);
    }, context: new WritableOverflowPageHeaderContext { page = page, pageId = pageId, nextPageId = nextPageId, payload = payload });

    public OverflowPageHeader ReadOverflowPageHeader(ReadOnlySpan<byte> page, ulong expectedPageId) =>
        TryCatch(operation: static context =>
    {
        ValidateOverflowPageHeaderOnRead(page: context.page, expectedPageId: context.expectedPageId);
        return ReadHeaderCore(page: context.page, expectedPageId: new PageId { Value = context.expectedPageId });
    }, context: new ReadableOverflowPageHeaderContext { page = page, expectedPageId = expectedPageId });

    public ReadOnlyMemory<byte> ReadOverflowPageHeaderPayload(ReadOnlySpan<byte> page, ulong expectedPageId) =>
        TryCatch(operation: static context =>
    {
        ValidateOverflowPageHeaderOnRead(page: context.page, expectedPageId: context.expectedPageId);
        return ReadPayloadCore(page: context.page, expectedPageId: new PageId { Value = context.expectedPageId });
    }, context: new ReadableOverflowPageHeaderContext { page = page, expectedPageId = expectedPageId });

    private static int GetPayloadCapacityCore(int pageSize)
    {
        if (!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(pageSize)))
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        return pageSize - OverflowPageFormat.HeaderLength;
    }

    private static void InitializeCore(Span<byte> page, PageId pageId, PageId? nextPageId, ReadOnlySpan<byte> payload)
    {
        page.Clear();
        pageHeaders.WritePageHeader(destination: page, pageHeader: new PageHeader { PageId = pageId, PageType = PageType.Overflow, FormatVersion = new sql_storage_engine.Models.Pages.PageFormatVersion { Value = sql_storage_engine.Models.Pages.PageHeaderFormat.CurrentVersion }, PageLogSequenceNumber = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 0 }, ChecksumAlgorithm = PageChecksumAlgorithm.Crc32, Checksum = 0 });
        page[OverflowPageFormat.NextPageOffset] = nextPageId.HasValue ? (byte)1 : (byte)0;

        BinaryPrimitives.WriteUInt64LittleEndian(destination: page[(OverflowPageFormat.NextPageOffset + 1)..], value: nextPageId.GetValueOrDefault()
            .Value);

        BinaryPrimitives.WriteUInt32LittleEndian(destination: page[OverflowPageFormat.UsedLengthOffset..], value: checked((uint)payload.Length));
        payload.CopyTo(destination: page[OverflowPageFormat.HeaderLength..]);
        pageHeaders.WriteChecksum(page: page, pageSize: page.Length);
    }

    private static OverflowPageHeader ReadHeaderCore(ReadOnlySpan<byte> page, PageId expectedPageId)
    {
        var capacity = GetPayloadCapacityCore(pageSize: page.Length);
        pageHeaders.ValidateChecksum(page: page, pageSize: page.Length);
        pageHeaders.CheckPageHeader(pageHeader: pageHeaders.ReadPageHeader(source: page), expectedPageId: (expectedPageId).Value, expectedType: PageType.Overflow);

        if (ContainsNonZero(bytes: page[45..OverflowPageFormat.HeaderLength]))
        {
            throw new StorageFormatException("Reserved overflow-header bytes must be zero.");
        }

        PageId? next = page[OverflowPageFormat.NextPageOffset] switch
        {
            0 when BinaryPrimitives.ReadUInt64LittleEndian(source: page[(OverflowPageFormat.NextPageOffset + 1)..]) == 0 => null,
            1 => new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = BinaryPrimitives.ReadUInt64LittleEndian(source: page[(OverflowPageFormat.NextPageOffset + 1)..])
            },
            _ => throw new StorageFormatException("Invalid nullable overflow next-page encoding.")};

        if (next?.Value == 0 || next == expectedPageId)
        {
            throw new StorageCorruptionException("Overflow page has an invalid next-page ID.");
        }

        var usedLength = BinaryPrimitives.ReadUInt32LittleEndian(source: page[OverflowPageFormat.UsedLengthOffset..]);

        if (usedLength == 0 || usedLength > capacity)
        {
            throw new StorageCorruptionException("Overflow used length is outside the payload region.");
        }

        return new OverflowPageHeader
        {
            NextPageId = next,
            UsedLength = usedLength
        };
    }

    private static ReadOnlyMemory<byte> ReadPayloadCore(ReadOnlySpan<byte> page, PageId expectedPageId)
    {
        var header = ReadHeaderCore(page: page, expectedPageId: expectedPageId);

        return page.Slice(start: OverflowPageFormat.HeaderLength, length: checked((int)header.UsedLength))
            .ToArray();
    }

    private static bool ContainsNonZero(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            if (value != 0)
            {
                return true;
            }
        }

        return false;
    }
}