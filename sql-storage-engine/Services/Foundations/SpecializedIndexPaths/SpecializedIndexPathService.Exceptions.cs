// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Foundations.SpecializedIndexPaths;
internal sealed partial class SpecializedIndexPathService
{
    private static void TryCatch(Action operation)
    {
        try
        {
            operation();
        }
        catch (InvalidDataException exception)
        {
            throw new SpecializedIndexPathValidationException(innerException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(innerException: exception);
        }
        catch (IOException exception)
        {
            throw new StorageModelDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }
}