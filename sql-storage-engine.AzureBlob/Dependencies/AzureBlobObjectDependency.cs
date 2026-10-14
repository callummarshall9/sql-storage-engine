// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace sql_storage_engine.AzureBlob.Dependencies;
internal sealed class AzureBlobObjectDependency : IAsyncDisposable
{
    private readonly BlobContainerClient container;
    private readonly object sync = new object();
    private readonly TaskCompletionSource drained = new TaskCompletionSource(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
    private int active;
    private bool closed;
    private readonly int transferSize;
    private readonly int uploadConcurrency;
    public AzureBlobObjectDependency(object? containerSource, int transferSize, int uploadConcurrency)
    {
        container = containerSource as BlobContainerClient ?? throw new ArgumentException(message: "An Azure BlobContainerClient is required.", paramName: nameof(containerSource));
        ArgumentOutOfRangeException.ThrowIfNegative(value: transferSize);
        ArgumentOutOfRangeException.ThrowIfNegative(value: uploadConcurrency);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value: transferSize, other: 16 * 1024 * 1024);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value: uploadConcurrency, other: 32);
        this.transferSize = transferSize == 0 ? 4 * 1024 * 1024 : transferSize;
        this.uploadConcurrency = uploadConcurrency == 0 ? 4 : uploadConcurrency;
    }

    public async ValueTask<(byte[] Bytes, string Version)?> ReadMetadataAsync(string key, int maximumBytes, CancellationToken cancellationToken)
    {
        EnterOperation();

        try
        {
            try
            {
                var result = await container.GetBlobClient(blobName: key).DownloadStreamingAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                using var response = result.GetRawResponse();
                await using var content = result.Value.Content;
                var length = result.Value.Details.ContentLength;

                if (length < 0 || length > maximumBytes)
                {
                    throw new InvalidDataException(message: "Azure Blob metadata exceeds its configured bound.");
                }

                var bytes = new byte[checked((int)length)];
                await content.ReadExactlyAsync(buffer: bytes, cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                var extra = new byte[1];

                if (await content.ReadAsync(buffer: extra, cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false) != 0)
                {
                    throw new InvalidDataException(message: "Azure Blob metadata exceeds its declared length.");
                }

                return (bytes, result.Value.Details.ETag.ToString());
            }
            catch (RequestFailedException exception) when (exception.Status == 404)
            {
                return null;
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    public async ValueTask DownloadAsync(string key, object destination, CancellationToken cancellationToken)
    {
        EnterOperation();

        try
        {
            var result = await container.GetBlobClient(blobName: key).DownloadStreamingAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            using var response = result.GetRawResponse();
            await using var content = result.Value.Content;
            await content.CopyToAsync(destination: (Stream)destination, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            ExitOperation();
        }
    }

    public async ValueTask UploadAsync(string key, object source, CancellationToken cancellationToken)
    {
        EnterOperation();

        try
        {
            var result = await container.GetBlobClient(blobName: key).UploadAsync(content: (Stream)source, options: new BlobUploadOptions
            {
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                TransferOptions = new StorageTransferOptions { InitialTransferSize = transferSize, MaximumTransferSize = transferSize, MaximumConcurrency = uploadConcurrency }
            }, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            using var response = result.GetRawResponse();
        }
        finally
        {
            ExitOperation();
        }
    }

    public async ValueTask<string> PublishMetadataAsync(string key, byte[] bytes, string? expectedVersion, CancellationToken cancellationToken)
    {
        EnterOperation();

        try
        {
            var conditions = expectedVersion is null ? new BlobRequestConditions { IfNoneMatch = ETag.All } : new BlobRequestConditions { IfMatch = new ETag(etag: expectedVersion) };
            var result = await container.GetBlobClient(blobName: key).UploadAsync(content: BinaryData.FromBytes(data: bytes), options: new BlobUploadOptions { Conditions = conditions }, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
            using var response = result.GetRawResponse();
            return result.Value.ETag.ToString();
        }
        finally
        {
            ExitOperation();
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (sync)
        {
            closed = true;

            if (active == 0)
            {
                drained.TrySetResult();
            }
        }

        await drained.Task.ConfigureAwait(continueOnCapturedContext: false);
    }

    private void EnterOperation()
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(condition: closed, instance: this);
            active = checked(active + 1);
        }
    }

    private void ExitOperation()
    {
        lock (sync)
        {
            active--;

            if (closed && active == 0)
            {
                drained.TrySetResult();
            }
        }
    }
}