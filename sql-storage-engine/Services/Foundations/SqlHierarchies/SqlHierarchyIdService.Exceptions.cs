// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Foundations.SqlHierarchies;
internal sealed partial class SqlHierarchyIdService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new StorageModelRangeValidationException(exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (IOException exception)
        {
            throw new StorageModelDependencyException(exception);
        }
        catch (FormatException exception)
        {
            throw new SqlHierarchyFormatException(innerException: exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new SqlHierarchyOperationException(innerException: exception);
        }
        catch (sql_storage_engine.Storage.StorageFormatException exception)
        {
            throw new sql_storage_engine.Storage.StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }
}