// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Text;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Foundations.SqlCollations;
internal sealed partial class SqlCollationService
{
    private static void TryCatch(Action operation)
    {
        try
        {
            operation();
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

    private static T TryCatch<T>(Func<ReadableSqlCollationContext, T> operation, ReadableSqlCollationContext context)
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

    private static void TryCatch(Action<WritableSqlCollationTextContext> operation, WritableSqlCollationTextContext context)
    {
        try
        {
            operation(obj: context);
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