// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Exceptions;
internal sealed class SqlHierarchyOperationException : InvalidOperationException
{
    public SqlHierarchyOperationException(InvalidOperationException innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}