// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Transactions;
using System.Threading;

namespace sql_storage_engine.Services.Foundations.TransactionDirectories;
internal sealed partial class TransactionDirectoryService
{
    private static void ValidateTransactionFileOnDelete(string path) =>
        Validate(inputs: [(string.IsNullOrWhiteSpace(value: path), path is null, nameof(path))]);

    private static void ValidateTransactionDirectory(string path) =>
        Validate(inputs: [(string.IsNullOrWhiteSpace(value: path), path is null, nameof(path))]);

    private static void ValidateTransactionFileNameOnGet(string path) =>
        Validate(inputs: [(string.IsNullOrWhiteSpace(value: path), path is null, nameof(path))]);

    private static void ValidateSiblingTransactionFilesOnGet(string path) =>
        Validate(inputs: [(string.IsNullOrWhiteSpace(value: path), path is null, nameof(path))]);

    private static void ValidateTransactionMetadata(string path, TransactionMetadata transactionMetadata, CancellationToken cancellationToken)
    {
        Validate(inputs: [(string.IsNullOrWhiteSpace(value: path), path is null, nameof(path)), (transactionMetadata is null, true, nameof(transactionMetadata))]);
        Validate(inputs: [(transactionMetadata!.Identity.DatabaseId.Value == Guid.Empty || transactionMetadata.Identity.Value == Guid.Empty, false, nameof(transactionMetadata)), (transactionMetadata.State is not (null or StorageTransactionState.Committed or StorageTransactionState.Aborted), false, nameof(transactionMetadata))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateTransactionMetadataOnGet(string path, bool receipt, CancellationToken cancellationToken)
    {
        Validate(inputs: [(string.IsNullOrWhiteSpace(value: path), path is null, nameof(path))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void Validate((bool Invalid, bool Missing, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (!input.Invalid)
            {
                continue;
            }

            if (input.Missing)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }

            throw new ArgumentException(message: "A transaction-file path is required.", paramName: input.ParameterName);
        }
    }
}