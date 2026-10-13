// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Exceptions;
internal sealed class SqlTypeDocumentValidationException : ArgumentException
{
    public SqlTypeDocumentValidationException(string message, string parameterName, Exception innerException) : base(message: message, paramName: parameterName, innerException: innerException)
    {
    }
}