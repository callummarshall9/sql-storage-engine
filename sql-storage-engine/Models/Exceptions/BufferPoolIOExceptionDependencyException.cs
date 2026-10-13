// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.IO;

namespace sql_storage_engine.Models.Exceptions;
public sealed class BufferPoolIOExceptionDependencyException : IOException
{
    public BufferPoolIOExceptionDependencyException(IOException innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}