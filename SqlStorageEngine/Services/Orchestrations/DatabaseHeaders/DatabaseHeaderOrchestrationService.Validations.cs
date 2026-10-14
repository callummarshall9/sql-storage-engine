// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Pages;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Orchestrations.DatabaseHeaders;
internal sealed partial class DatabaseHeaderOrchestrationService
{
    private static void ValidateDatabaseHeaderOnWrite(Span<byte> page, DatabaseHeader databaseHeader)
    {
        ArgumentNullException.ThrowIfNull(argument: databaseHeader);
        Validate(inputs: [(page.Length != databaseHeader.PageSize, "Buffer length must equal database page size.")]);
    }

    private static void ValidateDatabaseHeaderSource(ReadOnlySpan<byte> page) =>
        Validate(inputs: [(page.Length < sql_storage_engine.Models.Pages.PageConstants.MinimumSize, "Database header page is truncated.")]);

    private static void Validate(ReadOnlySpan<(bool Invalid, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new StorageFormatException(message: input.Message);
            }
        }
    }
}