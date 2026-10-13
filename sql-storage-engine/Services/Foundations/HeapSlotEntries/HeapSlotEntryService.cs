// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Heap;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.HeapSlotEntries;
internal sealed partial class HeapSlotEntryService : IHeapSlotEntryService
{
    public HeapSlotEntry ReadHeapSlotEntry(ReadOnlySpan<byte> page, ushort slotIndex, ushort slotCount, uint rowDataStart) =>
        TryCatch(operation: static context =>
    {
        ValidateHeapSlotEntrySource(page: context.page, slotIndex: context.slotIndex, slotCount: context.slotCount, rowDataStart: context.rowDataStart);
        return ReadCore(page: context.page, slotIndex: context.slotIndex, rowDataStart: context.rowDataStart);
    }, context: new ReadableHeapSlotEntryContext { page = page, slotIndex = slotIndex, slotCount = slotCount, rowDataStart = rowDataStart });

    public void WriteHeapSlotEntry(Span<byte> page, ushort slotIndex, HeapSlotEntry heapSlotEntry) =>
        TryCatch(operation: static context =>
    {
        ValidateHeapSlotEntryOnWrite(page: context.page, slotIndex: context.slotIndex, heapSlotEntry: context.heapSlotEntry);
        WriteCore(page: context.page, slotIndex: context.slotIndex, slot: context.heapSlotEntry);
    }, context: new WritableHeapSlotEntryContext { page = page, slotIndex = slotIndex, heapSlotEntry = heapSlotEntry });

    public int CalculateHeapSlotEntryOffset(ushort slotIndex) =>
        TryCatch(operation: () =>
    {
        ValidateHeapSlotEntryIndex(slotIndex: slotIndex);
        return checked(HeapPageFormat.HeaderLength + slotIndex * HeapPageFormat.SlotEntryLength);
    });

    private static HeapSlotEntry ReadCore(ReadOnlySpan<byte> page, ushort slotIndex, uint rowDataStart)
    {
        var source = page.Slice(start: checked(HeapPageFormat.HeaderLength + slotIndex * HeapPageFormat.SlotEntryLength), length: HeapPageFormat.SlotEntryLength);
        var state = (HeapSlotState)BinaryPrimitives.ReadUInt16LittleEndian(source: source);

        if (!Enum.IsDefined(value: state))
        {
            throw new StorageFormatException($"Unknown heap slot state {(ushort)state}.");
        }

        if (BinaryPrimitives.ReadUInt16LittleEndian(source: source[2..]) != 0)
        {
            throw new StorageFormatException("Reserved slot bytes must be zero.");
        }

        var entry = new HeapSlotEntry
        {
            State = state,
            Offset = BinaryPrimitives.ReadUInt32LittleEndian(source: source[4..]),
            Length = BinaryPrimitives.ReadUInt32LittleEndian(source: source[8..]),
            Generation = new sql_storage_engine.Models.Identifiers.SlotGeneration
            {
                Value = BinaryPrimitives.ReadUInt32LittleEndian(source: source[12..])
            }
        };

        if (state == HeapSlotState.Live)
        {
            var end = checked((ulong)entry.Offset + entry.Length);

            if (entry.Length == 0 || entry.Offset < rowDataStart || end > (ulong)page.Length)
            {
                throw new StorageCorruptionException($"Live slot {slotIndex} has an invalid row range.");
            }
        }
        else if (entry.Offset != 0 || entry.Length != 0)
        {
            throw new StorageCorruptionException($"Non-live slot {slotIndex} must not reference row bytes.");
        }

        return entry;
    }

    private static void WriteCore(Span<byte> page, ushort slotIndex, HeapSlotEntry slot)
    {
        var destination = page.Slice(start: checked(HeapPageFormat.HeaderLength + slotIndex * HeapPageFormat.SlotEntryLength), length: HeapPageFormat.SlotEntryLength);
        destination.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(destination: destination, value: (ushort)slot.State);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: destination[4..], value: slot.Offset);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: destination[8..], value: slot.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: destination[12..], value: slot.Generation.Value);
    }
}