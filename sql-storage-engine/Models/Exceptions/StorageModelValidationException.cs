// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Exceptions;
/// <summary>Preserves the validation failure reported by a model-processing service.</summary>
public sealed class StorageModelValidationException : ArgumentException
{
    public StorageModelValidationException(ArgumentException innerException) : base(message: innerException.Message, paramName: innerException.ParamName, innerException: innerException)
    {
    }
}