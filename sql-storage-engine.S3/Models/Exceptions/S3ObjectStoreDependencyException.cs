// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.S3.Models.Exceptions;
public sealed class S3ObjectStoreDependencyException(Exception innerException) : System.IO.IOException(message: innerException.Message, innerException: innerException)
{
}