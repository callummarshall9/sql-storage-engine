// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Foundations.StorageMetrics;
internal sealed partial class StorageMetricsService
{
    private static void TryCatch(Action operation)
    {
        try
        {
            operation();
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(exception);
        }
        catch (ObjectDisposedException exception)
        {
            throw new StorageModelDependencyException(exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(exception);
        }
    }
}