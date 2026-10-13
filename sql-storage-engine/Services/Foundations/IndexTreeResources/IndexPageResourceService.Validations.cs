// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Services.Foundations.IndexTreeResources;
internal sealed partial class IndexPageResourceService
{
    private static void ValidateResource()
    {
    }

    private static void ValidatePage(ulong pageId, CancellationToken cancellationToken)
    {
        Validate(inputs: [(pageId == 0, nameof(pageId))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateAllocation(ushort pageType, CancellationToken cancellationToken)
    {
        Validate(inputs: [(pageType != 4 && pageType != 5, nameof(pageType))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateConfiguration(IndexTreeResourceConfiguration indexTreeResourceConfiguration)
    {
        ArgumentNullException.ThrowIfNull(argument: indexTreeResourceConfiguration);
        ArgumentNullException.ThrowIfNull(argument: indexTreeResourceConfiguration.BufferSource);
        ArgumentNullException.ThrowIfNull(argument: indexTreeResourceConfiguration.AllocatorSource);
        ArgumentNullException.ThrowIfNull(argument: indexTreeResourceConfiguration.PageSize);
        ArgumentNullException.ThrowIfNull(argument: indexTreeResourceConfiguration.Pin);
        ArgumentNullException.ThrowIfNull(argument: indexTreeResourceConfiguration.Allocate);
    }

    private static void ValidateLease(IndexPageLease indexPageLease)
    {
        ArgumentNullException.ThrowIfNull(argument: indexPageLease);
        Validate(inputs: [(indexPageLease.Id == Guid.Empty, nameof(indexPageLease))]);
    }

    private static void ValidateLeaseMark(IndexPageLease indexPageLease, ulong logSequenceNumber)
    {
        ValidateLease(indexPageLease: indexPageLease);
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentOutOfRangeException(paramName: input.ParameterName);
            }
        }
    }
}