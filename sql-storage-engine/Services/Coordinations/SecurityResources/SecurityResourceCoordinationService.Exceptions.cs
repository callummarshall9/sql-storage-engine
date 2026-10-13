// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading.Tasks;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Security;

namespace sql_storage_engine.Services.Coordinations.SecurityResources;
internal sealed partial class SecurityResourceCoordinationService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (Exception exception)when (IsKnownException(exception: exception))
        {
            var mappedException = MapKnownException(caughtException: exception);

            if (ReferenceEquals(objA: exception, objB: mappedException))
            {
                throw;
            }

            throw mappedException;
        }
        catch (StorageResourceException exception)
        {
            throw new WalDependencyException(innerException: exception);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new StorageModelRangeValidationException(innerException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(innerException: exception);
        }
        catch (System.IO.IOException exception)
        {
            throw new WalIOExceptionDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }

    private static async ValueTask<T> TryCatch<T>(Func<ValueTask<T>> operation)
    {
        try
        {
            return await operation()
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception exception)when (IsKnownException(exception: exception))
        {
            var mappedException = MapKnownException(caughtException: exception);

            if (ReferenceEquals(objA: exception, objB: mappedException))
            {
                throw;
            }

            throw mappedException;
        }
        catch (StorageResourceException exception)
        {
            throw new WalDependencyException(innerException: exception);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new StorageModelRangeValidationException(innerException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(innerException: exception);
        }
        catch (System.IO.IOException exception)
        {
            throw new WalIOExceptionDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }

    private static bool IsKnownException(Exception exception) =>
        exception is sql_storage_engine.Security.StorageAuditException or StorageAuthorizationException or InvalidDatabaseMagicException or InvalidPageSizeException or UnsupportedDatabaseVersionException or StorageCorruptionException or StorageFormatException or StorageResourceExhaustedException or OperationCanceledException or ObjectDisposedException or InvalidOperationException or OverflowException;

    private static Exception MapKnownException(Exception caughtException) =>
        caughtException switch
    {
        sql_storage_engine.Security.StorageAuditException => caughtException,
        StorageAuthorizationException exception => new StorageAuthorizationException(reason: exception.Message),
        InvalidDatabaseMagicException exception => new InvalidDatabaseMagicException(innerException: exception),
        InvalidPageSizeException exception => new InvalidPageSizeException(pageSize: exception.PageSize, innerException: exception),
        UnsupportedDatabaseVersionException exception => new UnsupportedDatabaseVersionException(version: exception.Version, innerException: exception),
        StorageCorruptionException exception => new StorageCorruptionException(message: exception.Message, innerException: exception),
        StorageFormatException exception => new StorageFormatException(message: exception.Message, innerException: exception),
        StorageResourceExhaustedException exception => new StorageResourceExhaustedException(message: exception.Message, innerException: exception),
        OperationCanceledException exception => new OperationCanceledException(message: exception.Message, innerException: exception, token: exception.CancellationToken),
        ObjectDisposedException exception => new ObjectDisposedException(message: exception.Message, innerException: exception),
        InvalidOperationException exception => new InvalidOperationException(message: exception.Message, innerException: exception),
        OverflowException exception => new OverflowException(message: exception.Message, innerException: exception),
        _ => throw new ArgumentException(message: "Unsupported exception mapping.", paramName: nameof(caughtException))};
}