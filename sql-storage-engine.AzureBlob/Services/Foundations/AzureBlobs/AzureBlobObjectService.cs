// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.AzureBlob.Brokers;
using sql_storage_engine.AzureBlob.Models;

namespace sql_storage_engine.AzureBlob.Services.Foundations.AzureBlobs;
internal sealed partial class AzureBlobObjectService(AzureBlobObjectBroker broker) : IAzureBlobObjectService
{
    internal static AzureBlobObjectService CreateAzureBlobObjectService(AzureBlobStorageConfiguration azureBlobStorageConfiguration) =>
        new AzureBlobObjectService(broker: new AzureBlobObjectBroker(configuration: azureBlobStorageConfiguration));

    public ValueTask<AzureBlobObjectContext> ReadAzureBlobObjectContextAsync(AzureBlobObjectContext azureBlobObjectContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateReadContext(azureBlobObjectContext: azureBlobObjectContext, cancellationToken: cancellationToken);

        var result = await broker.ReadMetadataAsync(key: azureBlobObjectContext.Key!, maximumBytes: azureBlobObjectContext.MaximumBytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        azureBlobObjectContext.Metadata = result;
        return azureBlobObjectContext;
    });

    public ValueTask DownloadAzureBlobObjectContextAsync(AzureBlobObjectContext azureBlobObjectContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateDownloadContext(azureBlobObjectContext: azureBlobObjectContext, cancellationToken: cancellationToken);

        await broker.DownloadAsync(key: azureBlobObjectContext.Key!, destination: azureBlobObjectContext.Destination!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask UploadAzureBlobObjectContextAsync(AzureBlobObjectContext azureBlobObjectContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateUploadContext(azureBlobObjectContext: azureBlobObjectContext, cancellationToken: cancellationToken);

        await broker.UploadAsync(key: azureBlobObjectContext.Key!, source: azureBlobObjectContext.Source!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<AzureBlobObjectContext> PublishAzureBlobObjectContextAsync(AzureBlobObjectContext azureBlobObjectContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePublishContext(azureBlobObjectContext: azureBlobObjectContext, cancellationToken: cancellationToken);

        azureBlobObjectContext.Version = await broker.PublishMetadataAsync(key: azureBlobObjectContext.Key!, bytes: azureBlobObjectContext.Bytes!, expectedVersion: azureBlobObjectContext.ExpectedVersion, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return azureBlobObjectContext;
    });

    public ValueTask CloseAzureBlobObjectContextAsync(AzureBlobObjectContext azureBlobObjectContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCloseContext(azureBlobObjectContext: azureBlobObjectContext, cancellationToken: cancellationToken);

        await broker.CloseAsync()
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}