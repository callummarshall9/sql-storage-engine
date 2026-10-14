// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Dependencies.ObjectStorageResources;

namespace sql_storage_engine.Brokers.ObjectStorageResources;
internal sealed class ObjectStorageResourceBroker : IObjectStorageResourceBroker
{
    private readonly StorageObjectResourceConfiguration? configuration;

    public ObjectStorageResourceBroker(StorageObjectResourceConfiguration? configuration)
    {
        this.configuration = configuration;
    }

    public async ValueTask<(byte[] Bytes, string Version)?> ReadAsync(string key, int maximumBytes, CancellationToken cancellationToken)
    {
        using var resource = CreateObjectStorageResourceDependency();

        return await resource.ReadAsync(key: key, maximumBytes: maximumBytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public async ValueTask<string> PublishAsync(string key, byte[] bytes, string? expectedVersion, CancellationToken cancellationToken)
    {
        using var resource = CreateObjectStorageResourceDependency();

        return await resource.PublishAsync(key: key, bytes: bytes, expectedVersion: expectedVersion, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public async ValueTask UploadAsync((string Key, byte[] Bytes)[] objects, int concurrency, CancellationToken cancellationToken)
    {
        using var resource = CreateObjectStorageResourceDependency();

        await resource.UploadAsync(objects: objects, concurrency: concurrency, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public byte[] CalculateChecksum(ReadOnlySpan<byte> bytes)
    {
        using var resource = CreateObjectStorageResourceDependency();

        return resource.CalculateChecksum(bytes: bytes);
    }

    public Guid CreateIdentity()
    {
        using var resource = CreateObjectStorageResourceDependency();

        return resource.CreateIdentity();
    }

    private ObjectStorageResourceDependency CreateObjectStorageResourceDependency() =>
        new ObjectStorageResourceDependency(read: configuration?.Read, upload: configuration?.Upload, publish: configuration?.Publish);
}