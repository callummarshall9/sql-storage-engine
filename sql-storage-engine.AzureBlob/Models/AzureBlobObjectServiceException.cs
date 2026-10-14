// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.AzureBlob.Models;
public sealed class AzureBlobObjectServiceException(Exception innerException) : Exception(message: "Azure Blob object storage operation failed.", innerException: innerException)
{
}