// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Foundations.SqlExpressionEnvironments;
internal sealed partial class SqlExpressionHostService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (InvalidDatabaseMagicException exception)
        {
            throw new InvalidDatabaseMagicException(innerException: exception);
        }
        catch (InvalidPageSizeException exception)
        {
            throw new InvalidPageSizeException(pageSize: exception.PageSize, innerException: exception);
        }
        catch (UnsupportedDatabaseVersionException exception)
        {
            throw new UnsupportedDatabaseVersionException(version: exception.Version, innerException: exception);
        }
        catch (StorageCorruptionException exception)
        {
            throw new StorageCorruptionException(message: exception.Message, innerException: exception);
        }
        catch (StorageFormatException exception)
        {
            throw new StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (StorageResourceExhaustedException exception)
        {
            throw new StorageResourceExhaustedException(message: exception.Message, innerException: exception);
        }
        catch (StorageResourceException exception)
        {
            throw new WalDependencyException(innerException: exception);
        }
        catch (OperationCanceledException exception)
        {
            throw new OperationCanceledException(message: exception.Message, innerException: exception, token: exception.CancellationToken);
        }
        catch (ObjectDisposedException exception)
        {
            throw new ObjectDisposedException(message: exception.Message, innerException: exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(message: exception.Message, innerException: exception);
        }
        catch (FormatException exception)
        {
            throw new FormatException(message: exception.Message, innerException: exception);
        }
        catch (DivideByZeroException exception)
        {
            throw new DivideByZeroException(message: exception.Message, innerException: exception);
        }
        catch (OverflowException exception)
        {
            throw new OverflowException(message: exception.Message, innerException: exception);
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
}