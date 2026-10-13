// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Text;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Foundations.JsonPayloads;
internal sealed partial class JsonPayloadService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (Exception exception)when (IsKnownException(exception: exception))
        {
            throw MapKnownException(caughtException: exception);
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

    private static T TryCatch<T>(Func<ReadableJsonPayloadContext, T> operation, ReadableJsonPayloadContext context)
    {
        try
        {
            return operation(arg: context);
        }
        catch (Exception exception)when (IsKnownException(exception: exception))
        {
            throw MapKnownException(caughtException: exception);
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

    private static bool IsKnownException(Exception exception) =>
        exception is StorageFormatException or InvalidDataException or DecoderFallbackException or JsonPayloadFormatException or InvalidOperationException;

    private static Exception MapKnownException(Exception caughtException) =>
        caughtException switch
    {
        StorageFormatException exception => new StorageFormatException(message: exception.Message, innerException: exception),
        InvalidDataException exception => new StorageFormatException(message: "Invalid native JSON payload.", innerException: exception),
        DecoderFallbackException exception => new DecoderFallbackException(message: exception.Message, innerException: exception),
        JsonPayloadFormatException exception => new JsonPayloadFormatException(innerException: exception),
        InvalidOperationException exception => new JsonPayloadFormatException(innerException: exception),
        _ => throw new ArgumentException(message: "Unsupported exception mapping.", paramName: nameof(caughtException))};
}