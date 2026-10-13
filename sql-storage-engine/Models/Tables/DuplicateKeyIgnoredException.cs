// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Tables;
public sealed class DuplicateKeyIgnoredException : InvalidOperationException
{
    public DuplicateKeyIgnoredException(string message) : base(message)
    {
    }

    public DuplicateKeyIgnoredException(string message, Exception innerException) : base(message, innerException)
    {
    }
}