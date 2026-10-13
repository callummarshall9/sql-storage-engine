// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.DatabaseHeaders;
internal sealed partial class SqlCollationDatabaseHeaderProcessingService
{
    private static void TryCatch(Action<WritableDatabaseHeaderContext> operation, WritableDatabaseHeaderContext context)
    {
        try
        {
            operation(obj: context);
        }
        catch (Exception exception)when (IsKnownException(exception: exception))
        {
            throw MapKnownException(caughtException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (IOException exception)
        {
            throw new StorageModelDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }

    private static T TryCatch<T>(Func<ReadableDatabaseHeaderContext, T> operation, ReadableDatabaseHeaderContext context)
    {
        try
        {
            return operation(arg: context);
        }
        catch (Exception exception)when (IsKnownException(exception: exception))
        {
            throw MapKnownException(caughtException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (IOException exception)
        {
            throw new StorageModelDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }

    private static bool IsKnownException(Exception exception) =>
        exception is InvalidDatabaseMagicException or InvalidPageSizeException or UnsupportedDatabaseVersionException or StorageCorruptionException or StorageFormatException;

    private static Exception MapKnownException(Exception caughtException) =>
        caughtException switch
    {
        InvalidDatabaseMagicException exception => new InvalidDatabaseMagicException(exception),
        InvalidPageSizeException exception => new InvalidPageSizeException(exception.PageSize, exception),
        UnsupportedDatabaseVersionException exception => new UnsupportedDatabaseVersionException(exception.Version, exception),
        StorageCorruptionException exception => new StorageCorruptionException(message: exception.Message, innerException: exception),
        StorageFormatException exception => new StorageFormatException(message: exception.Message, innerException: exception),
        _ => throw new ArgumentException(message: "Unsupported exception mapping.", paramName: nameof(caughtException))};
}