// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Exceptions;
/// <summary>Preserves a rejected lock conversion as a validation failure.</summary>
public sealed class StorageLockValidationException : InvalidOperationException
{
    public StorageLockValidationException(InvalidOperationException innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}