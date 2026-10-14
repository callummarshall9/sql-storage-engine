// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Exceptions;
public sealed class StorageModelNullValidationException : ArgumentNullException
{
    private readonly string? parameterName;
    public StorageModelNullValidationException(ArgumentNullException innerException) : base(message: innerException.Message, innerException: innerException)
    {
        parameterName = innerException.ParamName;
    }

    public override string? ParamName => parameterName;
}