// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Exceptions;
internal sealed class SqlDecimalFormatException : FormatException
{
    public SqlDecimalFormatException(FormatException innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}