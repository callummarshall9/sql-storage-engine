// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.AzureBlob.Models;
public sealed class AzureBlobStorageConfiguration
{
    public object? ContainerSource { get; set; }
    public int TransferSize { get; set; }
    public int UploadConcurrency { get; set; }
}