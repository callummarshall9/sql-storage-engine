// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Backup;
public sealed class WrongDatabaseWalException : StorageException
{
    public WrongDatabaseWalException(string message) : base(message: message)
    {
    }

    public WrongDatabaseWalException(string message, Exception innerException) : base(message: message, innerException: innerException)
    {
    }
}