// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Text.Json;

namespace sql_storage_engine.Models.Exceptions;
public sealed class JsonPayloadFormatException : JsonException
{
    public JsonPayloadFormatException(Exception innerException) : base(message: innerException.Message, innerException: innerException)
    {
    }

    public JsonPayloadFormatException(string message) : base(message: message)
    {
    }
}