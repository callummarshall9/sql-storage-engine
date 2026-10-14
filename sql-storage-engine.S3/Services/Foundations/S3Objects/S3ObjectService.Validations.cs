// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.S3.Models.S3Objects;

namespace sql_storage_engine.S3.Services.Foundations.S3Objects;
internal sealed partial class S3ObjectService
{
    private static void ValidateBinding(object client, string bucket)
    {
        ArgumentNullException.ThrowIfNull(argument: client);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: bucket);
    }

    private static void ValidateContext(S3ObjectContext s3ObjectContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: s3ObjectContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: s3ObjectContext.Key);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateRead(S3ObjectContext s3ObjectContext, CancellationToken cancellationToken)
    {
        ValidateContext(s3ObjectContext: s3ObjectContext, cancellationToken: cancellationToken);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value: s3ObjectContext.MaximumBytes);
    }

    private static void ValidateDownload(S3ObjectContext s3ObjectContext, CancellationToken cancellationToken)
    {
        ValidateContext(s3ObjectContext: s3ObjectContext, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(argument: s3ObjectContext.Stream);

        if (!s3ObjectContext.Stream.CanWrite)
        {
            throw new ArgumentException(message: "The S3 download destination must be writable.");
        }
    }

    private static void ValidateUpload(S3ObjectContext s3ObjectContext, CancellationToken cancellationToken)
    {
        ValidateContext(s3ObjectContext: s3ObjectContext, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(argument: s3ObjectContext.Stream);

        if (!s3ObjectContext.Stream.CanRead)
        {
            throw new ArgumentException(message: "The S3 upload source must be readable.");
        }
    }

    private static void ValidatePublish(S3ObjectContext s3ObjectContext, CancellationToken cancellationToken)
    {
        ValidateContext(s3ObjectContext: s3ObjectContext, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(argument: s3ObjectContext.Bytes);

        if (s3ObjectContext.ExpectedVersion is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(argument: s3ObjectContext.ExpectedVersion);
        }
    }
}