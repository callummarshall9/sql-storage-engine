// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Exceptions;
public sealed class CrashRunValidationException : InvalidOperationException
{
    public CrashRunValidationException(InvalidOperationException innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}