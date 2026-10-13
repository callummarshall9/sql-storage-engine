// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.IO;

namespace sql_storage_engine.Models.Exceptions;
/// <summary>Preserves invalid durable transaction-receipt evidence.</summary>
public sealed class TransactionReceiptValidationException : IOException
{
    public TransactionReceiptValidationException(string message) : base(message: message, innerException: new InvalidDataException(message))
    {
    }
}