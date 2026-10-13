// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Exceptions;
/// <summary>Preserves cancellation of a transaction metadata operation and its token.</summary>
public sealed class TransactionMetadataCancellationException : OperationCanceledException
{
    public TransactionMetadataCancellationException(OperationCanceledException innerException) : base(message: innerException.Message, innerException: innerException, token: innerException.CancellationToken)
    {
    }
}