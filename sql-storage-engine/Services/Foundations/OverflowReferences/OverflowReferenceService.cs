// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Overflow;

namespace sql_storage_engine.Services.Foundations.OverflowReferences;
internal sealed partial class OverflowReferenceService : IOverflowReferenceService
{
    public void WriteOverflowReference(Span<byte> destination, OverflowReference overflowReference) =>
        TryCatch(operation: static context =>
    {
        ValidateOverflowReferenceOnWrite(destination: context.destination, overflowReference: context.overflowReference);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: context.destination, value: context.overflowReference.FirstPageId.Value);
        BinaryPrimitives.WriteInt64LittleEndian(destination: context.destination[8..], value: context.overflowReference.TotalLength);
    }, context: new WritableOverflowReferenceContext { destination = destination, overflowReference = overflowReference });

    public OverflowReference ReadOverflowReference(ReadOnlySpan<byte> source) =>
        TryCatch(operation: static context =>
    {
        ValidateOverflowReferenceSource(source: context.source);

        var overflowReference = new OverflowReference
        {
            FirstPageId = new PageId
            {
                Value = BinaryPrimitives.ReadUInt64LittleEndian(source: context.source)
            },
            TotalLength = BinaryPrimitives.ReadInt64LittleEndian(source: context.source[8..])
        };

        ValidateOverflowReference(overflowReference: overflowReference);
        return overflowReference;
    }, context: new ReadableOverflowReferenceContext { source = source });

    public void CheckOverflowReference(OverflowReference overflowReference) =>
        TryCatch(operation: () =>
    {
        ValidateOverflowReference(overflowReference: overflowReference);
    });
}