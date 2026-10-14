// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Overflow;

namespace sql_storage_engine.Services.Processings.OverflowPageHeaders;
internal sealed partial class PageHeaderOverflowPageHeaderProcessingService
{
    private static void ValidateOverflowPageHeaderSize(int pageSize) =>
        Validate(inputs: [(!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(pageSize)), "Unsupported overflow page size.")]);

    private static void ValidateOverflowPageHeaderOnWrite(Span<byte> page, ulong pageId, ulong? nextPageId, ReadOnlySpan<byte> payload)
    {
        Validate(inputs: [(!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(page.Length)), "Unsupported overflow page size."), (pageId == 0, "Overflow pages cannot use page zero."), (nextPageId == 0 || nextPageId == pageId, "Invalid next overflow page ID."), (payload.IsEmpty || payload.Length > page.Length - OverflowPageFormat.HeaderLength, "Overflow payload must be nonempty and fit on the page.")]);
    }

    private static void ValidateOverflowPageHeaderOnRead(ReadOnlySpan<byte> page, ulong expectedPageId) =>
        Validate(inputs: [(!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(page.Length)), "Unsupported overflow page size."), (expectedPageId == 0, "Overflow pages cannot use page zero.")]);

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