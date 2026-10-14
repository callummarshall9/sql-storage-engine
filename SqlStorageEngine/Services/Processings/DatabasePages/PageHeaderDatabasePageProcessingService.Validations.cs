// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Services.Processings.DatabasePages;
internal sealed partial class PageHeaderDatabasePageProcessingService
{
    private static void ValidateDatabasePageHeader(ReadOnlySpan<byte> page, int pageSize) =>
        Validate(inputs: [(page.Length != pageSize || !System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(pageSize)), "Database page must match a supported page size.")]);

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