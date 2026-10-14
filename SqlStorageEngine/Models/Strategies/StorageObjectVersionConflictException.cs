// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Models.Strategies;
/// <summary>The remote commit changed since it was opened. Publication must stop rather than overwrite another writer.</summary>
public class StorageObjectVersionConflictException : StorageException
{
    public StorageObjectVersionConflictException(string message, Exception? innerException = null) : base(message: message, innerException: innerException)
    {
    }
}