// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.S3.Models.Configurations;
internal sealed class S3ObjectConfiguration
{
    public object? Client { get; set; }
    public string? Bucket { get; set; }
}