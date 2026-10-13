// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.BackupFiles;
internal sealed class BackupFileDependency : TimeProvider
{
    private readonly TimeProvider timeProvider;
    private readonly JsonSerializerOptions options = new JsonSerializerOptions
    {
        WriteIndented = true
    };
    public BackupFileDependency(TimeProvider? timeProvider)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string GetFullPath(string path)
    {
        return Path.GetFullPath(path: path);
    }

    public string CombinePath(string directory, string name)
    {
        return Path.Combine(path1: directory, path2: name);
    }

    public string GetFileName(string path)
    {
        return Path.GetFileName(path: path);
    }

    public bool Exists(string path)
    {
        return File.Exists(path: path) || Directory.Exists(path: path);
    }

    public long? ReadLength(string path)
    {
        var file = new FileInfo(fileName: path);
        return file.Exists ? file.Length : null;
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path: path);
    }

    public void DeleteDirectory(string path)
    {
        Directory.Delete(path: path, recursive: true);
    }

    public override DateTimeOffset GetUtcNow()
    {
        return timeProvider.GetUtcNow();
    }

    public byte[] Serialize(object value, Type type)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value: value, inputType: type, options: options);
    }

    public object? Deserialize(byte[] value, Type type)
    {
        return JsonSerializer.Deserialize(utf8Json: value, returnType: type, options: options);
    }

    public async ValueTask<byte[]> ReadBytesAsync(string path, CancellationToken cancellationToken)
    {
        return await File.ReadAllBytesAsync(path: path, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public async ValueTask WriteBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        await File.WriteAllBytesAsync(path: path, bytes: bytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public async ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(path: source, mode: FileMode.Open, access: FileAccess.Read, share: FileShare.Read, bufferSize: 81920, options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = new FileStream(path: destination, mode: FileMode.CreateNew, access: FileAccess.Write, share: FileShare.None, bufferSize: 81920, options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        await input.CopyToAsync(destination: output, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await output.FlushAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        output.Flush(flushToDisk: true);
    }

    public async ValueTask<(long Size, string Sha256)> DescribeAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path: path, mode: FileMode.Open, access: FileAccess.Read, share: FileShare.Read, bufferSize: 81920, options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var checksum = await SHA256.HashDataAsync(source: stream, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return (stream.Length, Convert.ToHexString(inArray: checksum));
    }

    public async ValueTask<byte[]> ReadPageAsync(string path, ulong pageId, int pageSize, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path: path, mode: FileMode.Open, access: FileAccess.Read, share: FileShare.Read, bufferSize: 81920, options: FileOptions.Asynchronous | FileOptions.RandomAccess);
        stream.Position = checked((long)pageId * pageSize);
        var bytes = new byte[pageSize];

        await stream.ReadExactlyAsync(buffer: bytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return bytes;
    }

    public bool AreHashesEqual(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(left: Convert.FromHexString(s: left), right: Convert.FromHexString(s: right));

    public string ReadAssemblyVersion(Type type) =>
        type.Assembly.GetName()
            .Version?.ToString() ?? "0.0.0";
}