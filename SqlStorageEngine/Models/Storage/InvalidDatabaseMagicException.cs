// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Storage;
public sealed class InvalidDatabaseMagicException : StorageFormatException
{
    public InvalidDatabaseMagicException(Exception? innerException = null) : base("The file does not contain the SQL storage-engine magic number.", innerException)
    {
    }
}