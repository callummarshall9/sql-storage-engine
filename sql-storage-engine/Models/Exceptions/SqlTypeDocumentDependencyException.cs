// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.IO;

namespace sql_storage_engine.Models.Exceptions;
internal sealed class SqlTypeDocumentDependencyException : IOException
{
    public SqlTypeDocumentDependencyException(IOException innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}