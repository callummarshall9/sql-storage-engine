// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Exposures.Strategies;
/// <summary>Stores immutable segments and conditionally publishes a mutable commit manifest.</summary>
public interface IStorageObjectStoreExposure
{
    ValueTask<(byte[] Bytes, string Version)?> ReadMetadataAsync(string key, int maximumBytes, CancellationToken cancellationToken = default);
    ValueTask DownloadAsync(string key, Stream destination, CancellationToken cancellationToken = default);
    ValueTask UploadAsync(string key, Stream source, CancellationToken cancellationToken = default);
    ValueTask<string> PublishMetadataAsync(string key, byte[] bytes, string? expectedVersion, CancellationToken cancellationToken = default);
}