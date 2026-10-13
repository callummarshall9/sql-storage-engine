// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;

namespace sql_storage_engine.Services.Foundations.TransactionDirectories;
internal sealed partial class TransactionDirectoryService
{
    private static void ValidateDatabasePath(string path) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: path);

    private static void ValidateDatabasePublication(string temporaryPath, string fullPath)
    {
        ValidateDatabasePath(path: temporaryPath);
        ValidateDatabasePath(path: fullPath);
    }

    private static void ValidateDatabasePrefix(string path, int length, CancellationToken cancellationToken)
    {
        ValidateDatabasePath(path: path);

        if (length < 0 || length > 1048576)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(length));
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}