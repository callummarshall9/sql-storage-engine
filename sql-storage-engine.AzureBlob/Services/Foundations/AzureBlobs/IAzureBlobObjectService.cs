// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.AzureBlob.Models;

namespace sql_storage_engine.AzureBlob.Services.Foundations.AzureBlobs;
internal interface IAzureBlobObjectService
{
    ValueTask<AzureBlobObjectContext> ReadAzureBlobObjectContextAsync(AzureBlobObjectContext azureBlobObjectContext, CancellationToken cancellationToken);

    ValueTask DownloadAzureBlobObjectContextAsync(AzureBlobObjectContext azureBlobObjectContext, CancellationToken cancellationToken);

    ValueTask UploadAzureBlobObjectContextAsync(AzureBlobObjectContext azureBlobObjectContext, CancellationToken cancellationToken);

    ValueTask<AzureBlobObjectContext> PublishAzureBlobObjectContextAsync(AzureBlobObjectContext azureBlobObjectContext, CancellationToken cancellationToken);

    ValueTask CloseAzureBlobObjectContextAsync(AzureBlobObjectContext azureBlobObjectContext, CancellationToken cancellationToken);
}