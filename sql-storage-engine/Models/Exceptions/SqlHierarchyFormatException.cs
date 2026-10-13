// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Exceptions;
internal sealed class SqlHierarchyFormatException : FormatException
{
    public SqlHierarchyFormatException(FormatException innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}