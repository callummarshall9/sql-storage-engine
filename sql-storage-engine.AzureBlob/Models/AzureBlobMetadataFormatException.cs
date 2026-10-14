// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;

namespace sql_storage_engine.AzureBlob.Models;
public sealed class AzureBlobMetadataFormatException(Exception innerException) : IOException(message: innerException.Message, innerException: innerException)
{
}