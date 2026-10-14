// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.UpgradeSerialization;
internal sealed class UpgradeSerializationDependency : IDisposable
{
    private IncrementalHash? hash;
    private FileStream? input;
    public byte[] CalculateChecksum(ReadOnlySpan<byte> bytes)
    {
        hash ??= IncrementalHash.CreateHash(hashAlgorithm: HashAlgorithmName.SHA256);
        hash.AppendData(data: bytes);
        return hash.GetHashAndReset();
    }

    public async ValueTask<(byte[]? Bytes, string? Error)> ReadBoundedAsync(string path, int expectedLength, CancellationToken cancellationToken)
    {
        input = new FileStream(path: path, mode: FileMode.Open, access: FileAccess.Read, share: FileShare.Read, bufferSize: 4096, options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        if (input.Length != expectedLength)
        {
            return (null, "Upgrade progress has an invalid file length.");
        }

        var bytes = new byte[expectedLength];

        try
        {
            await input.ReadExactlyAsync(buffer: bytes, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (EndOfStreamException)
        {
            return (null, "Upgrade progress is truncated.");
        }

        if (await input.ReadAsync(buffer: bytes.AsMemory(start: 0, length: 1), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false) != 0)
        {
            return (null, "Upgrade progress contains trailing bytes.");
        }

        return (bytes, null);
    }

    public void Dispose()
    {
        input?.Dispose();
        hash?.Dispose();
    }
}