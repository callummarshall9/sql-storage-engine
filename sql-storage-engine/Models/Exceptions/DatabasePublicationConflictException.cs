// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.IO;

namespace sql_storage_engine.Models.Exceptions;
public sealed class DatabasePublicationConflictException : IOException
{
    public DatabasePublicationConflictException(string path) : base(message: $"Database already exists: '{path}'.")
    {
    }
}