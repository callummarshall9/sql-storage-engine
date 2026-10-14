// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Strategies;

namespace sql_storage_engine.AzureBlob.Models;
public sealed class AzureBlobObjectVersionConflictException(Exception innerException) : StorageObjectVersionConflictException(message: "Azure Blob was changed by another writer.", innerException: innerException)
{
}