// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.AzureBlob.Dependencies;
using sql_storage_engine.AzureBlob.Models;

namespace sql_storage_engine.AzureBlob.Brokers;
internal sealed class AzureBlobObjectBroker : IAzureBlobObjectBroker
{
    private readonly AzureBlobObjectDependency dependency;
    public AzureBlobObjectBroker(AzureBlobStorageConfiguration configuration)
    {
        dependency = new AzureBlobObjectDependency(containerSource: configuration.ContainerSource, transferSize: configuration.TransferSize, uploadConcurrency: configuration.UploadConcurrency);
    }

    public ValueTask<(byte[] Bytes, string Version)?> ReadMetadataAsync(string key, int maximumBytes, CancellationToken cancellationToken) =>
        dependency.ReadMetadataAsync(key: key, maximumBytes: maximumBytes, cancellationToken: cancellationToken);

    public ValueTask DownloadAsync(string key, Stream destination, CancellationToken cancellationToken) =>
        dependency.DownloadAsync(key: key, destination: destination, cancellationToken: cancellationToken);

    public ValueTask UploadAsync(string key, Stream source, CancellationToken cancellationToken) =>
        dependency.UploadAsync(key: key, source: source, cancellationToken: cancellationToken);

    public ValueTask<string> PublishMetadataAsync(string key, byte[] bytes, string? expectedVersion, CancellationToken cancellationToken) =>
        dependency.PublishMetadataAsync(key: key, bytes: bytes, expectedVersion: expectedVersion, cancellationToken: cancellationToken);

    public ValueTask CloseAsync() =>
        dependency.DisposeAsync();
}