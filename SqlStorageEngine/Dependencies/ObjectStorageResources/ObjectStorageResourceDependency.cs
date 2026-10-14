// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.ObjectStorageResources;
internal sealed class ObjectStorageResourceDependency : IDisposable
{
    private readonly IncrementalHash checksum = IncrementalHash.CreateHash(hashAlgorithm: HashAlgorithmName.SHA256);

    private readonly object sync = new object();

    private readonly Func<string, int, CancellationToken, ValueTask<(byte[] Bytes, string Version)?>>? read;

    private readonly Func<string, Stream, CancellationToken, ValueTask>? upload;

    private readonly Func<string, byte[], string?, CancellationToken, ValueTask<string>>? publish;

    public ObjectStorageResourceDependency(Func<string, int, CancellationToken, ValueTask<(byte[] Bytes, string Version)?>>? read, Func<string, Stream, CancellationToken, ValueTask>? upload, Func<string, byte[], string?, CancellationToken, ValueTask<string>>? publish)
    {
        this.read = read;

        this.upload = upload;

        this.publish = publish;
    }

    public ValueTask<(byte[] Bytes, string Version)?> ReadAsync(string key, int maximumBytes, CancellationToken cancellationToken) =>
        read!(key, maximumBytes, cancellationToken);

    public ValueTask<string> PublishAsync(string key, byte[] bytes, string? expectedVersion, CancellationToken cancellationToken) =>
        publish!(key, bytes, expectedVersion, cancellationToken);

    public async ValueTask UploadAsync((string Key, byte[] Bytes)[] objects, int concurrency, CancellationToken cancellationToken)
    {
        await Parallel.ForEachAsync(source: objects, parallelOptions: new ParallelOptions { MaxDegreeOfParallelism = concurrency, CancellationToken = cancellationToken }, body: async (item, token) =>
        {
            using var stream = new MemoryStream(buffer: item.Bytes, writable: false);

            try
            {
                await upload!(item.Key, stream, token)
                    .ConfigureAwait(continueOnCapturedContext: false);
            }
            catch
            {
                var existing = await read!(item.Key, item.Bytes.Length, token)
                    .ConfigureAwait(continueOnCapturedContext: false);

                if (existing is not { } stored || !stored.Bytes.AsSpan()
                    .SequenceEqual(other: item.Bytes))
                {
                    throw;
                }
            }
        })
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public byte[] CalculateChecksum(ReadOnlySpan<byte> bytes)
    {
        lock (sync)
        {
            checksum.AppendData(data: bytes);

            return checksum.GetHashAndReset();
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            checksum.Dispose();
        }
    }

    public Guid CreateIdentity() =>
        Guid.NewGuid();
}