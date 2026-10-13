// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.IO;

namespace sql_storage_engine.Models.Exceptions;
/// <summary>Preserves a transaction-directory persistence failure.</summary>
public sealed class TransactionDirectoryDependencyException : IOException
{
    public TransactionDirectoryDependencyException(IOException innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}