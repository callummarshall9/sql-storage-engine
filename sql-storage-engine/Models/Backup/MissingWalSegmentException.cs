// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Backup;
public sealed class MissingWalSegmentException : StorageException
{
    public MissingWalSegmentException(string message) : base(message: message)
    {
    }

    public MissingWalSegmentException(string message, Exception innerException) : base(message: message, innerException: innerException)
    {
    }
}