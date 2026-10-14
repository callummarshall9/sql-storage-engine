// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.AzureBlob.Models;
using sql_storage_engine.AzureBlob.Services.Foundations.AzureBlobs;
using sql_storage_engine.Exposures.Strategies;

namespace sql_storage_engine.AzureBlob.Exposures;
public sealed class AzureBlobObjectStoreExposure : IStorageObjectStoreExposure, ICompositionExposure, IAsyncDisposable
{
    private readonly AzureBlobObjectService service;
    public AzureBlobObjectStoreExposure(AzureBlobStorageConfiguration configuration)
    {
        service = AzureBlobObjectService.CreateAzureBlobObjectService(azureBlobStorageConfiguration: configuration);
    }

    public async ValueTask<(byte[] Bytes, string Version)?> ReadMetadataAsync(string key, int maximumBytes, CancellationToken cancellationToken = default) =>
        (await service.ReadAzureBlobObjectContextAsync(azureBlobObjectContext: new AzureBlobObjectContext { Key = key, MaximumBytes = maximumBytes }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Metadata;

    public ValueTask DownloadAsync(string key, Stream destination, CancellationToken cancellationToken = default) =>
        service.DownloadAzureBlobObjectContextAsync(azureBlobObjectContext: new AzureBlobObjectContext { Key = key, Destination = destination }, cancellationToken: cancellationToken);

    public ValueTask UploadAsync(string key, Stream source, CancellationToken cancellationToken = default) =>
        service.UploadAzureBlobObjectContextAsync(azureBlobObjectContext: new AzureBlobObjectContext { Key = key, Source = source }, cancellationToken: cancellationToken);

    public async ValueTask<string> PublishMetadataAsync(string key, byte[] bytes, string? expectedVersion, CancellationToken cancellationToken = default) =>
        (await service.PublishAzureBlobObjectContextAsync(azureBlobObjectContext: new AzureBlobObjectContext { Key = key, Bytes = bytes, ExpectedVersion = expectedVersion }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Version!;

    public ValueTask DisposeAsync() =>
        service.CloseAzureBlobObjectContextAsync(azureBlobObjectContext: new AzureBlobObjectContext(), cancellationToken: CancellationToken.None);
}