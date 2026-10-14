// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;

namespace sql_storage_engine.S3.Dependencies.S3Objects;
internal sealed class S3ObjectDependency(object source, string bucket) : IDisposable
{
    private readonly IAmazonS3 client = (IAmazonS3)source;
    private GetObjectResponse? response;
    private MemoryStream? metadata;
    public static void CheckClient(object source)
    {
        if (source is not IAmazonS3)
        {
            throw new ArgumentException(message: "The S3 resource binding requires an IAmazonS3 client.", paramName: nameof(source));
        }
    }

    public async ValueTask<(byte[] Bytes, string Version)?> ReadMetadataAsync(string key, int maximumBytes, CancellationToken cancellationToken)
    {
        try
        {
            response = await client.GetObjectAsync(request: new GetObjectRequest { BucketName = bucket, Key = key }, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (AmazonS3Exception exception)when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value: response.ETag))
        {
            throw new IOException(message: "S3 did not return a metadata version.");
        }

        if (response.ContentLength < 0 || response.ContentLength > maximumBytes)
        {
            throw new IOException(message: "S3 metadata exceeds its bounded length.");
        }

        var bytes = new byte[checked((int)response.ContentLength)];
        await response.ResponseStream.ReadExactlyAsync(buffer: bytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var extra = new byte[1];
        if (await response.ResponseStream.ReadAsync(buffer: extra, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false) != 0)
        {
            throw new IOException(message: "S3 metadata contains trailing bytes.");
        }

        return (bytes, response.ETag);
    }

    public async ValueTask DownloadAsync(string key, object destination, CancellationToken cancellationToken)
    {
        response = await client.GetObjectAsync(request: new GetObjectRequest { BucketName = bucket, Key = key }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await response.ResponseStream.CopyToAsync(destination: (Stream)destination, bufferSize: 81920, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public async ValueTask UploadAsync(string key, object source, CancellationToken cancellationToken)
    {
        await client.PutObjectAsync(request: new PutObjectRequest { BucketName = bucket, Key = key, InputStream = (Stream)source, AutoCloseStream = false, AutoResetStreamPosition = false, IfNoneMatch = "*" }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public async ValueTask<string> PublishMetadataAsync(string key, byte[] bytes, string? expectedVersion, CancellationToken cancellationToken)
    {
        metadata = new MemoryStream(buffer: bytes, writable: false);

        var result = await client.PutObjectAsync(request: new PutObjectRequest { BucketName = bucket, Key = key, InputStream = metadata, AutoCloseStream = false, AutoResetStreamPosition = false, IfMatch = expectedVersion, IfNoneMatch = expectedVersion is null ? "*" : null }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (string.IsNullOrWhiteSpace(value: result.ETag))
        {
            throw new IOException(message: "S3 did not return a metadata version.");
        }

        return result.ETag;
    }

    public void Dispose()
    {
        response?.Dispose();
        metadata?.Dispose();
    }
}