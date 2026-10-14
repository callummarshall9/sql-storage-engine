// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Threading.Tasks;
using Azure;
using sql_storage_engine.AzureBlob.Models;

namespace sql_storage_engine.AzureBlob.Services.Foundations.AzureBlobs;
internal sealed partial class AzureBlobObjectService
{
    private static async ValueTask<T> TryCatch<T>(Func<ValueTask<T>> operation)
    {
        try
        {
            return await operation()
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException)
        {
            throw;
        }
        catch (RequestFailedException exception) when (exception.Status == 412 || (exception.Status == 409 && exception.ErrorCode == "BlobAlreadyExists"))
        {
            throw new AzureBlobObjectVersionConflictException(innerException: exception);
        }
        catch (RequestFailedException exception)
        {
            throw new AzureBlobStorageDependencyException(innerException: exception);
        }
        catch (InvalidDataException exception)
        {
            throw new AzureBlobMetadataFormatException(innerException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new AzureBlobObjectValidationException(innerException: exception);
        }
        catch (IOException exception)
        {
            throw new AzureBlobStorageDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new AzureBlobObjectServiceException(innerException: exception);
        }
    }

    private static async ValueTask TryCatch(Func<ValueTask> operation)
    {
        try
        {
            await operation()
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException)
        {
            throw;
        }
        catch (RequestFailedException exception) when (exception.Status == 412 || (exception.Status == 409 && exception.ErrorCode == "BlobAlreadyExists"))
        {
            throw new AzureBlobObjectVersionConflictException(innerException: exception);
        }
        catch (RequestFailedException exception)
        {
            throw new AzureBlobStorageDependencyException(innerException: exception);
        }
        catch (InvalidDataException exception)
        {
            throw new AzureBlobMetadataFormatException(innerException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new AzureBlobObjectValidationException(innerException: exception);
        }
        catch (IOException exception)
        {
            throw new AzureBlobStorageDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new AzureBlobObjectServiceException(innerException: exception);
        }
    }
}