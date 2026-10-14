// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Services.Foundations.OverflowReferences;

namespace sql_storage_engine.Exposures.Overflow;
public static class OverflowReferenceExposure
{
    private static readonly IOverflowReferenceService service = new OverflowReferenceService();
    public static void WriteOverflowReference(Span<byte> destination, OverflowReference overflowReference) =>
        service.WriteOverflowReference(destination: destination, overflowReference: overflowReference);

    public static OverflowReference ReadOverflowReference(ReadOnlySpan<byte> source) =>
        service.ReadOverflowReference(source: source);

    public static void CheckOverflowReference(OverflowReference overflowReference) =>
        service.CheckOverflowReference(overflowReference: overflowReference);
}