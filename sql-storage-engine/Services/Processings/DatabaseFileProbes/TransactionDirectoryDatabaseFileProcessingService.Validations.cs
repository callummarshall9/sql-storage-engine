// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;

namespace sql_storage_engine.Services.Processings.DatabaseFileProbes;
internal sealed partial class TransactionDirectoryDatabaseFileProcessingService
{
    private static void ValidateProbe(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: path);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidatePath(string path) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: path);

    private static void ValidatePublication(string temporaryPath, string fullPath)
    {
        ValidatePath(path: temporaryPath);
        ValidatePath(path: fullPath);
    }
}