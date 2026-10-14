// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Text;
using System.Security.Cryptography;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.ObjectManifests;
internal sealed partial class StorageObjectManifestService
{
    private static T TryCatch<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (StorageFormatException exception)
        {
            throw new StorageFormatException(message: exception.Message, innerException: exception);
        }
        catch (EncoderFallbackException exception)
        {
            throw new StorageFormatException(message: "Object storage manifest text is not valid UTF-8.", innerException: exception);
        }
        catch (DecoderFallbackException exception)
        {
            throw new StorageFormatException(message: "Object storage manifest text is not valid UTF-8.", innerException: exception);
        }
        catch (ArgumentException exception)
        {
            throw new StorageModelValidationException(innerException: exception);
        }
        catch (CryptographicException exception)
        {
            throw new StorageIoDependencyException(innerException: exception);
        }
        catch (Exception exception)
        {
            throw new StorageModelServiceException(innerException: exception);
        }
    }
}