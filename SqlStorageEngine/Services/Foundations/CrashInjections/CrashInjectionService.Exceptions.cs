// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Foundations.CrashInjections;
internal sealed partial class CrashInjectionService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (SimulatedProcessTerminationException exception)
        {
            throw new SimulatedProcessTerminationException(crashTermination: exception.Termination, innerException: exception);
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

    private static void TryCatch(Action operation)
    {
        try
        {
            operation();
        }
        catch (SimulatedProcessTerminationException exception)
        {
            throw new SimulatedProcessTerminationException(crashTermination: exception.Termination, innerException: exception);
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