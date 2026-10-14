// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Net;
using sql_storage_engine.Models.Strategies;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using sql_storage_engine.S3.Models.Exceptions;

namespace sql_storage_engine.S3.Services.Foundations.S3Objects;
internal sealed partial class S3ObjectService
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
        catch (ArgumentException exception)
        {
            throw new S3ObjectStoreValidationException(innerException: exception);
        }
        catch (AmazonS3Exception exception)when (exception.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
        {
            throw new S3StorageObjectVersionConflictException(innerException: exception);
        }
        catch (AmazonS3Exception exception)
        {
            throw new S3ObjectStoreDependencyException(innerException: exception);
        }
        catch (IOException exception)
        {
            throw new S3ObjectStoreDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new S3ObjectStoreServiceException(innerException: exception);
        }
    }
}