// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Dependencies.ObjectStorageResources;

namespace sql_storage_engine.Brokers.ObjectStorageResources;
internal interface IObjectStorageResourceBroker
{
    ValueTask<(byte[] Bytes, string Version)?> ReadAsync(string key, int maximumBytes, CancellationToken cancellationToken);

    ValueTask<string> PublishAsync(string key, byte[] bytes, string? expectedVersion, CancellationToken cancellationToken);

    ValueTask UploadAsync((string Key, byte[] Bytes)[] objects, int concurrency, CancellationToken cancellationToken);

    byte[] CalculateChecksum(ReadOnlySpan<byte> bytes);

    Guid CreateIdentity();
}