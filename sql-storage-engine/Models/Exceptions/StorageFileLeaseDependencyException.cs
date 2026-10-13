// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;

namespace sql_storage_engine.Models.Exceptions;
public sealed class StorageFileLeaseDependencyException : IOException
{
    public StorageFileLeaseDependencyException(Exception innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}