// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.AzureBlob.Models;

namespace sql_storage_engine.AzureBlob.Services.Foundations.AzureBlobs;
internal sealed partial class AzureBlobObjectService
{
    private const int MaximumMetadataBytes = 16 * 1024 * 1024;

    private static void ValidateContext(AzureBlobObjectContext azureBlobObjectContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: azureBlobObjectContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: azureBlobObjectContext.Key);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateReadContext(AzureBlobObjectContext azureBlobObjectContext, CancellationToken cancellationToken)
    {
        ValidateContext(azureBlobObjectContext: azureBlobObjectContext, cancellationToken: cancellationToken);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value: azureBlobObjectContext.MaximumBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value: azureBlobObjectContext.MaximumBytes, other: MaximumMetadataBytes);
    }

    private static void ValidateDownloadContext(AzureBlobObjectContext azureBlobObjectContext, CancellationToken cancellationToken)
    {
        ValidateContext(azureBlobObjectContext: azureBlobObjectContext, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(argument: azureBlobObjectContext.Destination);
    }

    private static void ValidateUploadContext(AzureBlobObjectContext azureBlobObjectContext, CancellationToken cancellationToken)
    {
        ValidateContext(azureBlobObjectContext: azureBlobObjectContext, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(argument: azureBlobObjectContext.Source);
    }

    private static void ValidatePublishContext(AzureBlobObjectContext azureBlobObjectContext, CancellationToken cancellationToken)
    {
        ValidateContext(azureBlobObjectContext: azureBlobObjectContext, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(argument: azureBlobObjectContext.Bytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value: azureBlobObjectContext.Bytes.Length, other: MaximumMetadataBytes);

        if (azureBlobObjectContext.ExpectedVersion is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(argument: azureBlobObjectContext.ExpectedVersion);
        }
    }

    private static void ValidateCloseContext(AzureBlobObjectContext azureBlobObjectContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: azureBlobObjectContext);
        cancellationToken.ThrowIfCancellationRequested();
    }
}