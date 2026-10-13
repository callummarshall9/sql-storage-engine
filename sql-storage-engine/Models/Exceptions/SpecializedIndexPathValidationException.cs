// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Exceptions;
public sealed class SpecializedIndexPathValidationException : ArgumentException
{
    public SpecializedIndexPathValidationException(Exception innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}