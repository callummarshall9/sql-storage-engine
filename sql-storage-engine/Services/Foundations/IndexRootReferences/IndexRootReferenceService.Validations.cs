// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Configurations;

namespace sql_storage_engine.Services.Foundations.IndexRootReferences;
internal sealed partial class IndexRootReferenceService
{
    private static void ValidateIndexRootConfiguration(IndexRootConfiguration configuration) =>
        ArgumentNullException.ThrowIfNull(argument: configuration);

    private static void ValidateIndexRootPageIdOnUpdate(ulong rootPageId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(inputs: [(rootPageId == 0, "Index roots cannot use page zero.")]);
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