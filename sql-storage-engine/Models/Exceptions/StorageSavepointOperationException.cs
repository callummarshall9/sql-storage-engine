// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Exceptions;
internal sealed class StorageSavepointOperationException : InvalidOperationException
{
    public StorageSavepointOperationException(InvalidOperationException innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}