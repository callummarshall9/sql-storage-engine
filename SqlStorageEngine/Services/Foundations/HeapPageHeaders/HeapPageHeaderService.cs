// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Identifiers;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.HeapPageHeaders;
internal sealed partial class HeapPageHeaderService : IHeapPageHeaderService
{
    public HeapPageHeader ReadHeapPageHeader(ReadOnlySpan<byte> page) =>
        TryCatch(operation: static context =>
    {
        ValidateHeapPageHeaderSource(page: context.page);
        return ReadCore(page: context.page);
    }, context: new ReadableHeapPageHeaderContext { page = page });

    public void WriteHeapPageHeader(Span<byte> page, HeapPageHeader heapPageHeader) =>
        TryCatch(operation: static context =>
    {
        ValidateHeapPageHeaderOnWrite(page: context.page, heapPageHeader: context.heapPageHeader);
        WriteCore(page: context.page, header: context.heapPageHeader);
    }, context: new WritableHeapPageHeaderContext { page = page, heapPageHeader = heapPageHeader });

    private static HeapPageHeader ReadCore(ReadOnlySpan<byte> page)
    {
        if (ContainsNonZero(bytes: page[60..HeapPageFormat.HeaderLength]))
        {
            throw new StorageFormatException("Reserved heap-header bytes must be zero.");
        }

        var header = new HeapPageHeader
        {
            PreviousPageId = ReadOptionalPage(source: page[HeapPageFormat.PreviousPageOffset..]),
            NextPageId = ReadOptionalPage(source: page[HeapPageFormat.NextPageOffset..]),
            SlotCount = BinaryPrimitives.ReadUInt16LittleEndian(source: page[HeapPageFormat.SlotCountOffset..]),
            SlotDirectoryEnd = BinaryPrimitives.ReadUInt32LittleEndian(source: page[HeapPageFormat.SlotDirectoryEndOffset..]),
            RowDataStart = BinaryPrimitives.ReadUInt32LittleEndian(source: page[HeapPageFormat.RowDataStartOffset..])
        };

        ValidateHeapPageHeader(page: page, heapPageHeader: header);
        return header;
    }

    private static void WriteCore(Span<byte> page, HeapPageHeader header)
    {
        WriteOptionalPage(destination: page[HeapPageFormat.PreviousPageOffset..], pageId: header.PreviousPageId);
        WriteOptionalPage(destination: page[HeapPageFormat.NextPageOffset..], pageId: header.NextPageId);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: page[HeapPageFormat.SlotCountOffset..], value: header.SlotCount);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: page[HeapPageFormat.SlotDirectoryEndOffset..], value: header.SlotDirectoryEnd);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: page[HeapPageFormat.RowDataStartOffset..], value: header.RowDataStart);
    }

    private static void WriteOptionalPage(Span<byte> destination, PageId? pageId)
    {
        destination[0] = pageId.HasValue ? (byte)1 : (byte)0;

        BinaryPrimitives.WriteUInt64LittleEndian(destination: destination[1..], value: pageId.GetValueOrDefault()
            .Value);
    }

    private static PageId? ReadOptionalPage(ReadOnlySpan<byte> source) =>
        source[0] switch
    {
        0 when BinaryPrimitives.ReadUInt64LittleEndian(source: source[1..]) == 0 => null,
        1 => new sql_storage_engine.Models.Identifiers.PageId
        {
            Value = BinaryPrimitives.ReadUInt64LittleEndian(source: source[1..])
        },
        _ => throw new StorageFormatException("Invalid nullable heap-page link encoding.")};

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