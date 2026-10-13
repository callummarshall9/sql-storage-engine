// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models;

namespace sql_storage_engine.Services.Processings.TransactionOptions;
internal sealed partial class StorageTransactionOptionsProcessingService
{
    private static void ValidateStorageTransactionOptions(StorageTransactionOptions storageTransactionOptions)
    {
        ArgumentNullException.ThrowIfNull(argument: storageTransactionOptions);
        ValidateStorageTransactionOptions(timeout: storageTransactionOptions.Timeout, isolation: storageTransactionOptions.Isolation, maximumJournalBytes: storageTransactionOptions.MaximumJournalBytes);
    }

    private static void ValidateStorageTransactionOptions(TimeSpan timeout, StorageTransactionIsolation isolation, long maximumJournalBytes) =>
        Validate(inputs: [(timeout <= TimeSpan.Zero || timeout.Ticks > TimeSpan.TicksPerDay, nameof(timeout), timeout), (isolation != StorageTransactionIsolation.Serializable, nameof(isolation), isolation), (maximumJournalBytes <= 0 || maximumJournalBytes > StorageTransactionOptions.MaximumSupportedJournalBytes, nameof(maximumJournalBytes), maximumJournalBytes)]);

    private static void Validate((bool Invalid, string ParameterName, object Value)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentOutOfRangeException(paramName: input.ParameterName, actualValue: input.Value, message: "Unsupported storage transaction option.");
            }
        }
    }
}