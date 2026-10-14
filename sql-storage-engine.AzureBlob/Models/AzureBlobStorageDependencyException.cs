// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;

namespace sql_storage_engine.AzureBlob.Models;
public sealed class AzureBlobStorageDependencyException(Exception innerException) : IOException("Azure Blob storage operation failed.", innerException)
{
}