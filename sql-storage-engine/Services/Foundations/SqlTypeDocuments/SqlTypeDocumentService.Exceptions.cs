// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Foundations.SqlTypeDocuments;
internal sealed partial class SqlTypeDocumentService
{
    private static T TryCatch<T>(Func<T> operation, string requirement, string parameterName)
    {
        try
        {
            return operation();
        }
        catch (InvalidDataException exception)
        {
            throw new SqlTypeDocumentValidationException(message: requirement, parameterName: parameterName, innerException: exception);
        }
        catch (IOException exception)
        {
            throw new SqlTypeDocumentDependencyException(innerException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }

    private static void TryCatch(Action operation, string requirement, string parameterName)
    {
        try
        {
            operation();
        }
        catch (InvalidDataException exception)
        {
            throw new SqlTypeDocumentValidationException(message: requirement, parameterName: parameterName, innerException: exception);
        }
        catch (IOException exception)
        {
            throw new SqlTypeDocumentDependencyException(innerException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }
}