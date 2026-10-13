// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Pages;

namespace sql_storage_engine.Services.Processings.PhysicalPageChanges;
internal sealed partial class PhysicalPageChangeCodecProcessingService : IPhysicalPageChangeCodecProcessingService
{
    public byte[] WritePhysicalPageChange(PhysicalPageChange physicalPageChange) =>
        TryCatch(operation: () =>
    {
        ValidatePhysicalPageChange(physicalPageChange: physicalPageChange);
        var bytes = new byte[checked(PhysicalPageChangeFormat.HeaderLength + physicalPageChange.BeforeImage.Length + physicalPageChange.AfterImage.Length)];
        BinaryPrimitives.WriteUInt64LittleEndian(destination: bytes, value: physicalPageChange.PageId.Value);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: bytes.AsSpan(start: 8), value: (ushort)physicalPageChange.PageType);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: bytes.AsSpan(start: 12), value: checked((uint)physicalPageChange.BeforeImage.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(destination: bytes.AsSpan(start: 16), value: checked((uint)physicalPageChange.AfterImage.Length));
        physicalPageChange.BeforeImage.Span.CopyTo(destination: bytes.AsSpan(start: PhysicalPageChangeFormat.HeaderLength));
        physicalPageChange.AfterImage.Span.CopyTo(destination: bytes.AsSpan(start: PhysicalPageChangeFormat.HeaderLength + physicalPageChange.BeforeImage.Length));
        return bytes;
    });

    public PhysicalPageChange ReadPhysicalPageChange(ReadOnlySpan<byte> source) =>
        TryCatch(operation: static context =>
    {
        ValidatePhysicalPageChangeSource(source: context.source);
        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(source: context.source[12..]);

        return new PhysicalPageChange
        {
            PageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = BinaryPrimitives.ReadUInt64LittleEndian(source: context.source)
            },
            PageType = (PageType)BinaryPrimitives.ReadUInt16LittleEndian(source: context.source[8..]),
            BeforeImage = context.source.Slice(start: PhysicalPageChangeFormat.HeaderLength, length: length)
            .ToArray(),
            AfterImage = context.source.Slice(start: PhysicalPageChangeFormat.HeaderLength + length, length: length)
            .ToArray()
        };
    }, context: new ReadableWalFormatContext { source = source });
}