// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.AzureBlob.Models;
public sealed class AzureBlobObjectValidationException(ArgumentException innerException) : ArgumentException(message: innerException.Message, paramName: innerException.ParamName, innerException: innerException)
{
}