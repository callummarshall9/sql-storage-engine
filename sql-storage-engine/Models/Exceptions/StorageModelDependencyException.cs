// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Models.Exceptions;
/// <summary>Preserves an external-resource failure reported by a model-processing service.</summary>
public sealed class StorageModelDependencyException : StorageException
{
    public StorageModelDependencyException(Exception innerException) : base(message: "Model processing failed.", innerException: innerException)
    {
    }
}