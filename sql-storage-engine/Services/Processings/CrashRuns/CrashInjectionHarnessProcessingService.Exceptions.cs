// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Threading.Tasks;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Processings.CrashRuns;
internal sealed partial class CrashInjectionHarnessProcessingService
{
    private static async Task<T> TryCatch<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation()
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (OperationCanceledException exception)
        {
            throw new OperationCanceledException(message: exception.Message, innerException: exception, token: exception.CancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new CrashRunValidationException(innerException: exception);
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
}