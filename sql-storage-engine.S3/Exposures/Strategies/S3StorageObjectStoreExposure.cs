// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Exposures.Strategies;
using sql_storage_engine.S3.Models.S3Objects;
using sql_storage_engine.S3.Services.Foundations.S3Objects;

namespace sql_storage_engine.S3.Exposures.Strategies;
public sealed class S3StorageObjectStoreExposure : IStorageObjectStoreExposure
{
    private readonly S3ObjectService service;
    public S3StorageObjectStoreExposure(object client, string bucket)
    {
        service = S3ObjectService.CreateS3ObjectService(client: client, bucket: bucket);
    }

    public async ValueTask<(byte[] Bytes, string Version)?> ReadMetadataAsync(string key, int maximumBytes, CancellationToken cancellationToken = default) =>
        (await service.ReadS3ObjectContextAsync(s3ObjectContext: new S3ObjectContext { Key = key, MaximumBytes = maximumBytes }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Metadata;

    public ValueTask DownloadAsync(string key, Stream destination, CancellationToken cancellationToken = default) =>
        new(task: service.DownloadS3ObjectContextAsync(s3ObjectContext: new S3ObjectContext { Key = key, Stream = destination }, cancellationToken: cancellationToken)
            .AsTask());

    public ValueTask UploadAsync(string key, Stream source, CancellationToken cancellationToken = default) =>
        new(task: service.UploadS3ObjectContextAsync(s3ObjectContext: new S3ObjectContext { Key = key, Stream = source }, cancellationToken: cancellationToken)
            .AsTask());

    public async ValueTask<string> PublishMetadataAsync(string key, byte[] bytes, string? expectedVersion, CancellationToken cancellationToken = default) =>
        (await service.PublishS3ObjectContextAsync(s3ObjectContext: new S3ObjectContext { Key = key, Bytes = bytes, ExpectedVersion = expectedVersion }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Version!;
}