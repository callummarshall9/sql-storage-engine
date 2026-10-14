// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Dependencies.Directories;

namespace sql_storage_engine.Brokers.Directories;
internal sealed partial class TransactionDirectoryBroker
{
    public string NormalizeFilePath(string path)
    {
        using var dependency = new TransactionDirectoryDependency();
        return dependency.NormalizeFilePath(path: path);
    }

    public (string FullPath, string TemporaryPath, Guid DatabaseIdentity) PrepareDatabasePublication(string path)
    {
        using var dependency = new TransactionDirectoryDependency();
        return dependency.PrepareDatabasePublication(path: path);
    }

    public void PublishDatabaseFile(string temporaryPath, string fullPath)
    {
        using var dependency = new TransactionDirectoryDependency();
        dependency.PublishDatabaseFile(temporaryPath: temporaryPath, fullPath: fullPath);
    }

    public async ValueTask<byte[]> ReadFilePrefixAsync(string path, int length, CancellationToken cancellationToken)
    {
        using var dependency = new TransactionDirectoryDependency();

        return await dependency.ReadFilePrefixAsync(path: path, length: length, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }
}