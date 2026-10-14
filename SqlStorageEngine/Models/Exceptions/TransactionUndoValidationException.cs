// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Exceptions;
public sealed class TransactionUndoValidationException : InvalidOperationException
{
    public TransactionUndoValidationException(InvalidOperationException innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}