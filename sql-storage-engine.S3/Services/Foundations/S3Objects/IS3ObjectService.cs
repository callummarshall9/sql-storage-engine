// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.S3.Models.S3Objects;

namespace sql_storage_engine.S3.Services.Foundations.S3Objects;
internal interface IS3ObjectService
{
    ValueTask<S3ObjectContext> ReadS3ObjectContextAsync(S3ObjectContext s3ObjectContext, CancellationToken cancellationToken);
    ValueTask<S3ObjectContext> DownloadS3ObjectContextAsync(S3ObjectContext s3ObjectContext, CancellationToken cancellationToken);
    ValueTask<S3ObjectContext> UploadS3ObjectContextAsync(S3ObjectContext s3ObjectContext, CancellationToken cancellationToken);
    ValueTask<S3ObjectContext> PublishS3ObjectContextAsync(S3ObjectContext s3ObjectContext, CancellationToken cancellationToken);
}