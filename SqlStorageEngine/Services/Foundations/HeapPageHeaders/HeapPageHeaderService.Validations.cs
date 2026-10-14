// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Pages;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.HeapPageHeaders;
internal sealed partial class HeapPageHeaderService
{
    private static void ValidateHeapPageHeaderSource(ReadOnlySpan<byte> page) =>
        ValidateArguments(inputs: [(!Enum.IsDefined(value: (PageSize)page.Length), "Heap pages must use a supported complete page size.")]);

    private static void ValidateHeapPageHeaderOnWrite(Span<byte> page, HeapPageHeader heapPageHeader)
    {
        ValidateHeapPageHeaderSource(page: page);
        ValidateHeapPageHeader(page: page, heapPageHeader: heapPageHeader);
    }

    private static void ValidateHeapPageHeader(ReadOnlySpan<byte> page, HeapPageHeader heapPageHeader) =>
        Validate(inputs: [(heapPageHeader.SlotDirectoryEnd != (uint)(HeapPageFormat.HeaderLength + heapPageHeader.SlotCount * HeapPageFormat.SlotEntryLength), "Heap slot-directory boundary does not match its slot count."), (heapPageHeader.SlotDirectoryEnd > heapPageHeader.RowDataStart || heapPageHeader.RowDataStart > page.Length, "Heap slot and row regions overlap or exceed the page.")]);

    private static void Validate(ReadOnlySpan<(bool Invalid, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new StorageCorruptionException(message: input.Message);
            }
        }
    }

    private static void ValidateArguments(ReadOnlySpan<(bool Invalid, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: input.Message);
            }
        }
    }
}