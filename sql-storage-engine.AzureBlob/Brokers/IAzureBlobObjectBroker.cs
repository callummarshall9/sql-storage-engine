// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.AzureBlob.Brokers;
internal interface IAzureBlobObjectBroker
{
    ValueTask<(byte[] Bytes, string Version)?> ReadMetadataAsync(string key, int maximumBytes, CancellationToken cancellationToken);

    ValueTask DownloadAsync(string key, Stream destination, CancellationToken cancellationToken);

    ValueTask UploadAsync(string key, Stream source, CancellationToken cancellationToken);

    ValueTask<string> PublishMetadataAsync(string key, byte[] bytes, string? expectedVersion, CancellationToken cancellationToken);

    ValueTask CloseAsync();
}