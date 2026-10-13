// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;

namespace sql_storage_engine.Models.Exceptions;
internal sealed class TransactionRecoveryEvidenceException : IOException
{
    public TransactionRecoveryEvidenceException(string message) : base(message: message)
    {
    }

    public TransactionRecoveryEvidenceException(string message, Exception innerException) : base(message: message, innerException: innerException)
    {
    }
}