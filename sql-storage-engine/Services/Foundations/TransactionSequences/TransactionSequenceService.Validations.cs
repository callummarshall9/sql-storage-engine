// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Configurations;

namespace sql_storage_engine.Services.Foundations.TransactionSequences;
internal sealed partial class TransactionSequenceService
{
    private static void ValidateTransactionSequenceConfiguration(TransactionSequenceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(argument: configuration);

        if (configuration.LastAllocatedId > long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(paramName: "lastAllocatedId");
        }
    }
}