// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Models.Exceptions;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.PageRecoveries;
internal sealed partial class PageHeaderRecoveryProcessingService
{
    private static T TryCatch<T>(Func<PageRecoveryRequest, T> operation, PageRecoveryRequest context)
    {
        try
        {
            return operation(arg: context);
        }
        catch (UnrecoverablePageCorruptionException exception)
        {
            throw new UnrecoverablePageCorruptionException(message: exception.Message, innerException: exception);
        }
        catch (StorageFormatException exception)
        {
            throw new StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (StorageCorruptionException exception)
        {
            throw new StorageCorruptionException(message: exception.Message, innerException: exception);
        }
        catch (StorageModelDependencyException exception)
        {
            throw new StorageModelDependencyException(exception);
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