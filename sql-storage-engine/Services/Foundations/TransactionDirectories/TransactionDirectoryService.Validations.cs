// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
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

    private static void ValidateTransactionMetadata<T>(string path, T value, CancellationToken cancellationToken)
    {
        Validate(inputs: [(string.IsNullOrWhiteSpace(value: path), path is null, nameof(path)), (value is null, true, nameof(value))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateTransactionMetadataOnGet(string path, CancellationToken cancellationToken)
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