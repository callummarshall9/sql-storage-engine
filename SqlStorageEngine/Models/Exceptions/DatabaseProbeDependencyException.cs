// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.IO;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Models.Exceptions;
public sealed class DatabaseProbeDependencyException : StorageResourceException
{
    public DatabaseProbeDependencyException(string path, IOException innerException) : base(message: $"Failed to inspect database '{path}'.", innerException: innerException)
    {
    }
}