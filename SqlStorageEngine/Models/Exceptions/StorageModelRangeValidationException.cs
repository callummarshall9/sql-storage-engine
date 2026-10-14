// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Exceptions;
/// <summary>Retains range-validation metadata and the original failure.</summary>
public sealed class StorageModelRangeValidationException : ArgumentOutOfRangeException
{
    public StorageModelRangeValidationException(ArgumentOutOfRangeException innerException) : base(message: innerException.Message, innerException: innerException)
    {
        ParamName = innerException.ParamName;
        ActualValue = innerException.ActualValue;
    }

    public override string? ParamName { get; }
    public override object? ActualValue { get; }
}