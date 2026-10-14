// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.StorageRegistrations;
internal sealed partial class StorageRegistrationService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(innerException: exception);
        }
        catch (StorageResourceException exception)
        {
            throw new StorageIoDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }
}