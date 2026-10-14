// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;

namespace sql_storage_engine.Models.Exceptions;
public sealed class StorageIoDependencyException : IOException
{
    public StorageIoDependencyException(Exception innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}