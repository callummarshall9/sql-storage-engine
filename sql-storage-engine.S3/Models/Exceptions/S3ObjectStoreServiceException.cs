// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.S3.Models.Exceptions;
public sealed class S3ObjectStoreServiceException(Exception innerException) : Exception(message: innerException.Message, innerException: innerException)
{
}