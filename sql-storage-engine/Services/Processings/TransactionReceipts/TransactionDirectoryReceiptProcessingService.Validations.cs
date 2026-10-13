// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Processings.TransactionReceipts;
internal sealed partial class TransactionDirectoryReceiptProcessingService
{
    private static void ValidateReceiptRequest(string path, StorageTransactionReceipt storageTransactionReceipt, CancellationToken cancellationToken)
    {
        Validate(inputs: [(string.IsNullOrWhiteSpace(value: path), path is null, nameof(path)), (storageTransactionReceipt is null, true, nameof(storageTransactionReceipt))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateReceiptContent(StorageTransactionReceipt? storageTransactionReceipt, StorageTransactionReceipt expectedStorageTransactionReceipt)
    {
        if (storageTransactionReceipt is null || storageTransactionReceipt.Identity != expectedStorageTransactionReceipt.Identity || storageTransactionReceipt.State is not (StorageTransactionState.Committed or StorageTransactionState.Aborted))
        {
            throw new TransactionReceiptValidationException(message: "Transaction receipt is invalid.");
        }
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

            throw new ArgumentException(message: "A transaction receipt path is required.", paramName: input.ParameterName);
        }
    }
}