// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Text;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Services.Foundations.LeafIndexEntries;
internal sealed partial class LeafIndexEntryService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (EncoderFallbackException exception)
        {
            throw new EncoderFallbackException(message: exception.Message, innerException: exception);
        }
        catch (DecoderFallbackException exception)
        {
            throw new DecoderFallbackException(message: exception.Message, innerException: exception);
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

    private static T TryCatch<T>(Func<ReadableLeafIndexEntryContext, T> operation, ReadableLeafIndexEntryContext context)
    {
        try
        {
            return operation(arg: context);
        }
        catch (EncoderFallbackException exception)
        {
            throw new EncoderFallbackException(message: exception.Message, innerException: exception);
        }
        catch (DecoderFallbackException exception)
        {
            throw new DecoderFallbackException(message: exception.Message, innerException: exception);
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