// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Storage;
/// <summary>Indicates that a bounded storage resource has no available capacity.</summary>
public sealed class StorageResourceExhaustedException : StorageException
{
    public StorageResourceExhaustedException(string message) : base(message)
    {
    }

    public StorageResourceExhaustedException(string message, Exception innerException) : base(message: message, innerException: innerException)
    {
    }
}