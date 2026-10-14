// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Dependencies.BackupFiles;

namespace sql_storage_engine.Brokers.BackupFiles;
internal sealed class BackupFileBroker : IBackupFileBroker
{
    private readonly BackupFileDependency dependency;
    public BackupFileBroker(BackupConfiguration backupConfiguration)
    {
        dependency = new BackupFileDependency(timeProvider: backupConfiguration.TimeProviderSource as TimeProvider);
    }

    public string GetFullPath(string path) =>
        dependency.GetFullPath(path: path);

    public string CombinePath(string directory, string name) =>
        dependency.CombinePath(directory: directory, name: name);

    public string GetFileName(string path) =>
        dependency.GetFileName(path: path);

    public bool Exists(string path) =>
        dependency.Exists(path: path);

    public long? ReadLength(string path) =>
        dependency.ReadLength(path: path);

    public void CreateDirectory(string path) =>
        dependency.CreateDirectory(path: path);

    public void DeleteDirectory(string path) =>
        dependency.DeleteDirectory(path: path);

    public DateTimeOffset ReadUtcNow() =>
        dependency.GetUtcNow();

    public int CountUtf8Bytes(string value) =>
        dependency.CountUtf8Bytes(value: value);

    public byte[] EncodeUtf8(string value) =>
        dependency.EncodeUtf8(value: value);

    public string DecodeUtf8(byte[] bytes) =>
        dependency.DecodeUtf8(bytes: bytes);

    public byte[] HashBytes(byte[] bytes) =>
        dependency.HashBytes(bytes: bytes);

    public ValueTask<byte[]> ReadBytesAsync(string path, int maximumBytes, CancellationToken cancellationToken) =>
        dependency.ReadBytesAsync(path: path, maximumBytes: maximumBytes, cancellationToken: cancellationToken);

    public ValueTask WriteBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken) =>
        dependency.WriteBytesAsync(path: path, bytes: bytes, cancellationToken: cancellationToken);

    public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken) =>
        dependency.CopyAsync(source: source, destination: destination, cancellationToken: cancellationToken);

    public ValueTask<(long Size, string Sha256)> DescribeAsync(string path, CancellationToken cancellationToken) =>
        dependency.DescribeAsync(path: path, cancellationToken: cancellationToken);

    public ValueTask<byte[]> ReadPageAsync(string path, ulong pageId, int pageSize, CancellationToken cancellationToken) =>
        dependency.ReadPageAsync(path: path, pageId: pageId, pageSize: pageSize, cancellationToken: cancellationToken);

    public bool AreHashesEqual(string left, string right) =>
        dependency.AreHashesEqual(left: left, right: right);

    public string ReadAssemblyVersion(Type type) =>
        dependency.ReadAssemblyVersion(type: type);
}