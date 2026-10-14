// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Services.Foundations.PageAddresses;
internal sealed partial class PageAddressService
{
    private static void ValidatePageAddress(ulong pageId, int pageSize)
    {
        Validate(inputs: [(pageSize <= 0, "Page size must be positive.")]);
        _ = checked((long)checked(pageId * (ulong)pageSize));
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string Message)> inputs)
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