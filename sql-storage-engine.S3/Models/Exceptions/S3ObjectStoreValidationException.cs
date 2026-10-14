// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.S3.Models.Exceptions;
public sealed class S3ObjectStoreValidationException(Exception innerException) : ArgumentException(message: innerException.Message, innerException: innerException)
{
}