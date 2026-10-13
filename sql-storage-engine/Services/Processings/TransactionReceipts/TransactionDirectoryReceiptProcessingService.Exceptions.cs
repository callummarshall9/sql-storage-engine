// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Threading.Tasks;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Processings.TransactionReceipts;
internal sealed partial class TransactionDirectoryReceiptProcessingService
{
    private static void TryCatch(Action operation)
    {
        try
        {
            operation();
        }
        catch (TransactionReceiptValidationException)
        {
            throw;
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (IOException exception)
        {
            throw new TransactionDirectoryDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }

    private static async ValueTask TryCatch(Func<ValueTask> operation)
    {
        try
        {
            await operation()
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException exception)
        {
            throw new TransactionMetadataCancellationException(exception);
        }
        catch (TransactionReceiptValidationException)
        {
            throw;
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (IOException exception)
        {
            throw new TransactionDirectoryDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }

    private static async ValueTask<T> TryCatch<T>(Func<ValueTask<T>> operation)
    {
        try
        {
            return await operation()
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException exception)
        {
            throw new TransactionMetadataCancellationException(exception);
        }
        catch (TransactionReceiptValidationException)
        {
            throw;
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (IOException exception)
        {
            throw new TransactionDirectoryDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }
}