// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.S3.Models.Configurations;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.S3.Brokers.S3Objects;
using sql_storage_engine.S3.Models.S3Objects;

namespace sql_storage_engine.S3.Services.Foundations.S3Objects;
internal sealed partial class S3ObjectService(S3ObjectBroker broker) : IS3ObjectService
{
    internal static S3ObjectService CreateS3ObjectService(object client, string bucket)
    {
        ValidateBinding(client: client, bucket: bucket);
        return new S3ObjectService(broker: new S3ObjectBroker(s3ObjectConfiguration: new S3ObjectConfiguration { Client = client, Bucket = bucket }));
    }

    public ValueTask<S3ObjectContext> ReadS3ObjectContextAsync(S3ObjectContext s3ObjectContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRead(s3ObjectContext: s3ObjectContext, cancellationToken: cancellationToken);

        var result = await broker.ReadMetadataAsync(key: s3ObjectContext.Key!, maximumBytes: s3ObjectContext.MaximumBytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        s3ObjectContext.Metadata = result;
        return s3ObjectContext;
    });

    public ValueTask<S3ObjectContext> DownloadS3ObjectContextAsync(S3ObjectContext s3ObjectContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateDownload(s3ObjectContext: s3ObjectContext, cancellationToken: cancellationToken);

        await broker.DownloadAsync(key: s3ObjectContext.Key!, destination: s3ObjectContext.Stream!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return s3ObjectContext;
    });

    public ValueTask<S3ObjectContext> UploadS3ObjectContextAsync(S3ObjectContext s3ObjectContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateUpload(s3ObjectContext: s3ObjectContext, cancellationToken: cancellationToken);

        await broker.UploadAsync(key: s3ObjectContext.Key!, source: s3ObjectContext.Stream!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return s3ObjectContext;
    });

    public ValueTask<S3ObjectContext> PublishS3ObjectContextAsync(S3ObjectContext s3ObjectContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePublish(s3ObjectContext: s3ObjectContext, cancellationToken: cancellationToken);

        s3ObjectContext.Version = await broker.PublishMetadataAsync(key: s3ObjectContext.Key!, bytes: s3ObjectContext.Bytes!, expectedVersion: s3ObjectContext.ExpectedVersion, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return s3ObjectContext;
    });
}