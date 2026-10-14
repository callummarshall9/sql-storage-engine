// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.IO;

namespace sql_storage_engine.Models.Exceptions;
public sealed class WalIOExceptionDependencyException : IOException
{
    public WalIOExceptionDependencyException(IOException innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}