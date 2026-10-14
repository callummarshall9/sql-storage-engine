// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.S3.Models.Configurations;
using sql_storage_engine.S3.Dependencies.S3Objects;

namespace sql_storage_engine.S3.Brokers.S3Objects;
internal sealed class S3ObjectBroker : IS3ObjectBroker
{
    private readonly S3ObjectConfiguration configuration;
    public S3ObjectBroker(S3ObjectConfiguration s3ObjectConfiguration)
    {
        S3ObjectDependency.CheckClient(source: s3ObjectConfiguration.Client!);
        configuration = s3ObjectConfiguration;
    }

    public async ValueTask<(byte[] Bytes, string Version)?> ReadMetadataAsync(string key, int maximumBytes, CancellationToken cancellationToken)
    {
        using var resource = new S3ObjectDependency(source: configuration.Client!, bucket: configuration.Bucket!);

        return await resource.ReadMetadataAsync(key: key, maximumBytes: maximumBytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public async ValueTask DownloadAsync(string key, Stream destination, CancellationToken cancellationToken)
    {
        using var resource = new S3ObjectDependency(source: configuration.Client!, bucket: configuration.Bucket!);

        await resource.DownloadAsync(key: key, destination: destination, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public async ValueTask UploadAsync(string key, Stream source, CancellationToken cancellationToken)
    {
        using var resource = new S3ObjectDependency(source: configuration.Client!, bucket: configuration.Bucket!);

        await resource.UploadAsync(key: key, source: source, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public async ValueTask<string> PublishMetadataAsync(string key, byte[] bytes, string? expectedVersion, CancellationToken cancellationToken)
    {
        using var resource = new S3ObjectDependency(source: configuration.Client!, bucket: configuration.Bucket!);

        return await resource.PublishMetadataAsync(key: key, bytes: bytes, expectedVersion: expectedVersion, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }
}