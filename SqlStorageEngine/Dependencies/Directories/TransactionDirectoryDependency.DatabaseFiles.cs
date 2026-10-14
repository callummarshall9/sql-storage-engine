// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.Directories;
internal sealed partial class TransactionDirectoryDependency
{
    public string NormalizeFilePath(string path) =>
        Path.GetFullPath(path: path);

    public (string FullPath, string TemporaryPath, Guid DatabaseIdentity) PrepareDatabasePublication(string path)
    {
        var fullPath = Path.GetFullPath(path: path);
        var parent = Path.GetDirectoryName(path: fullPath)!;
        Directory.CreateDirectory(path: parent);
        var identity = Guid.NewGuid();
        var temporaryPath = Path.Combine(path1: parent, path2: $".{Path.GetFileName(path: fullPath)}.{identity:N}.tmp");
        return (fullPath, temporaryPath, identity);
    }

    public void PublishDatabaseFile(string temporaryPath, string fullPath)
    {
        File.Move(sourceFileName: temporaryPath, destFileName: fullPath, overwrite: false);
        FlushDirectory(path: fullPath);
    }

    public async ValueTask<byte[]> ReadFilePrefixAsync(string path, int length, CancellationToken cancellationToken)
    {
        using var handle = File.OpenHandle(path: path, mode: FileMode.Open, access: FileAccess.Read, share: FileShare.ReadWrite, options: FileOptions.Asynchronous | FileOptions.RandomAccess);
        var prefix = new byte[length];
        var total = 0;

        while (total < length)
        {
            var read = await RandomAccess.ReadAsync(handle: handle, buffer: prefix.AsMemory(start: total), fileOffset: total, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total == length ? prefix : prefix[..total];
    }
}