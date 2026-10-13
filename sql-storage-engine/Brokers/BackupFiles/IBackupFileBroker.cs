// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.BackupFiles;
internal interface IBackupFileBroker
{
    string GetFullPath(string path);

    string CombinePath(string directory, string name);

    string GetFileName(string path);

    bool Exists(string path);

    long? ReadLength(string path);

    void CreateDirectory(string path);

    void DeleteDirectory(string path);

    DateTimeOffset ReadUtcNow();

    byte[] Serialize(object value, Type type);

    object? Deserialize(byte[] value, Type type);

    ValueTask<byte[]> ReadBytesAsync(string path, CancellationToken cancellationToken);

    ValueTask WriteBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken);

    ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken);

    ValueTask<(long Size, string Sha256)> DescribeAsync(string path, CancellationToken cancellationToken);

    ValueTask<byte[]> ReadPageAsync(string path, ulong pageId, int pageSize, CancellationToken cancellationToken);

    bool AreHashesEqual(string left, string right);

    string ReadAssemblyVersion(Type type);
}