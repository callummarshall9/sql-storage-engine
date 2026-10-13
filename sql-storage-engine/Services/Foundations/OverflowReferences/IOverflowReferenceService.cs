// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Overflow;

namespace sql_storage_engine.Services.Foundations.OverflowReferences;
internal interface IOverflowReferenceService
{
    void WriteOverflowReference(Span<byte> destination, OverflowReference overflowReference);

    OverflowReference ReadOverflowReference(ReadOnlySpan<byte> source);

    void CheckOverflowReference(OverflowReference overflowReference);
}