// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Dependencies.Directories;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.Directories;
internal sealed partial class TransactionDirectoryBroker : ITransactionDirectoryBroker
{
    public void DeleteFile(string path)
    {
        using var directory = new TransactionDirectoryDependency();
        directory.DeleteFile(path: path);
    }

    public void FlushDirectory(string path)
    {
        using var directory = new TransactionDirectoryDependency();
        directory.FlushDirectory(path: path);
    }

    public string GetFileName(string path)
    {
        using var directory = new TransactionDirectoryDependency();
        return directory.GetFileName(path: path);
    }

    public IReadOnlyList<(string Path, string Name)> GetSiblingFiles(string path)
    {
        using var directory = new TransactionDirectoryDependency();
        return directory.GetSiblingFiles(path: path);
    }

    public bool FileExists(string path)
    {
        using var directory = new TransactionDirectoryDependency();
        return directory.FileExists(path: path);
    }

    public async ValueTask WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        using var directory = new TransactionDirectoryDependency();

        await directory.WriteJsonAsync(path: path, value: value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public async ValueTask<T?> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var directory = new TransactionDirectoryDependency();

        return await directory.ReadJsonAsync<T>(path: path, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public System.Guid GenerateIdentity()
    {
        using var directory = new TransactionDirectoryDependency();
        return directory.GenerateIdentity();
    }
}