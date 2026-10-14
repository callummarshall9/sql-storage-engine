// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Models.Exceptions;
/// <summary>Preserves an unexpected failure reported by a model-processing service.</summary>
public sealed class StorageModelServiceException : StorageException
{
    public StorageModelServiceException(Exception innerException) : base(message: "Model processing failed.", innerException: innerException)
    {
    }
}