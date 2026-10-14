// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Services.Foundations.IndexTreeResources;
internal sealed partial class IndexRootResourceService
{
    private static void ValidateResource()
    {
    }

    private static void ValidateConfiguration(IndexTreeResourceConfiguration indexTreeResourceConfiguration)
    {
        ArgumentNullException.ThrowIfNull(argument: indexTreeResourceConfiguration);

        if (indexTreeResourceConfiguration.RootContext is not null)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(argument: indexTreeResourceConfiguration.RootSource);
        ArgumentNullException.ThrowIfNull(argument: indexTreeResourceConfiguration.ReadRoot);
        ArgumentNullException.ThrowIfNull(argument: indexTreeResourceConfiguration.UpdateRoot);
    }

    private static void ValidateIndexRootResourceOnUpdate(ulong rootPageId, CancellationToken cancellationToken)
    {
        Validate(inputs: [(rootPageId == 0, nameof(rootPageId))]);
        cancellationToken.ThrowIfCancellationRequested();
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