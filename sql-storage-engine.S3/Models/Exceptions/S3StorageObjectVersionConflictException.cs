// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Strategies;

namespace sql_storage_engine.S3.Models.Exceptions;
public sealed class S3StorageObjectVersionConflictException(Exception innerException) : StorageObjectVersionConflictException(message: "The S3 object version changed before publication.", innerException: innerException)
{
}