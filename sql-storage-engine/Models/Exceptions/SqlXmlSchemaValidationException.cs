// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Xml.Schema;

namespace sql_storage_engine.Models.Exceptions;
internal sealed class SqlXmlSchemaValidationException : XmlSchemaException
{
    public SqlXmlSchemaValidationException(Exception innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }
}