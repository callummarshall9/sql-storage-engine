// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Diagnostics;
public sealed class UnrecoverablePageCorruptionException : StorageException
{
    public UnrecoverablePageCorruptionException(string message) : base(message: message)
    {
    }

    public UnrecoverablePageCorruptionException(string message, Exception innerException) : base(message: message, innerException: innerException)
    {
    }
}