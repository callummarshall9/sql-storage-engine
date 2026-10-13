// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Models.Exceptions;
public sealed class BackupDependencyException : StorageResourceException
{
    public BackupDependencyException(Exception innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}