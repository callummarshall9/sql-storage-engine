// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Exceptions;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.BackupSegments;
internal sealed partial class WalSegmentHeaderBackupFileContextProcessingService
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
        catch (sql_storage_engine.Backup.WrongDatabaseWalException exception)
        {
            throw new sql_storage_engine.Backup.WrongDatabaseWalException(message: exception.Message, innerException: exception);
        }
        catch (sql_storage_engine.Backup.MissingWalSegmentException exception)
        {
            throw new sql_storage_engine.Backup.MissingWalSegmentException(message: exception.Message, innerException: exception);
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
            throw new BackupDependencyException(innerException: exception);
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
        catch (System.IO.FileNotFoundException exception)
        {
            throw new BackupFileNotFoundDependencyException(message: exception.Message, fileName: exception.FileName, innerException: exception);
        }
        catch (System.IO.IOException exception)
        {
            throw new BackupIOExceptionDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }
}