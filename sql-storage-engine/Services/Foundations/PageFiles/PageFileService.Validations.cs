// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Configurations;

namespace sql_storage_engine.Services.Foundations.PageFiles;
internal sealed partial class PageFileService
{
    private static void ValidatePageFileConfiguration(PageFileConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(argument: configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: configuration.Path);
        Validate(inputs: [(!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(configuration.PageSize)), "Unsupported page size."), (configuration.ReadOnly && configuration.CreateNew, "New page files must be writable.")]);
    }

    private void ValidatePageFileOnRead(ulong pageId, Memory<byte> destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(inputs: [(destination.Length != configuration.PageSize, "Destination must match the page size.")]);
        _ = CalculatePageFileOffset(pageId: pageId, pageSize: configuration.PageSize);
    }

    private void ValidatePageFileOnWrite(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(inputs: [(source.Length != configuration.PageSize, "Source must match the page size.")]);
        _ = CalculatePageFileOffset(pageId: pageId, pageSize: configuration.PageSize);
    }

    private static void ValidatePageFileOnFlush(CancellationToken cancellationToken) =>
        cancellationToken.ThrowIfCancellationRequested();

    private static void ValidatePageFileLength(long length) =>
        Validate(inputs: [(length < 0, "File length cannot be negative.")]);

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