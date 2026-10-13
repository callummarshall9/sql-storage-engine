// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Services.Processings.DatabaseFileProbes;
internal interface ITransactionDirectoryDatabaseFileProcessingService
{
    ValueTask<int> ReadTransactionDirectoryDatabasePageSizeAsync(string path, CancellationToken cancellationToken);

    string NormalizeTransactionDirectoryDatabasePath(string path);

    (string FullPath, string TemporaryPath, Guid DatabaseIdentity) PrepareTransactionDirectoryDatabasePublication(string path);

    void PublishTransactionDirectoryDatabaseFile(string temporaryPath, string fullPath);

    void RemoveTransactionDirectoryDatabaseTemporaryFile(string path);
}