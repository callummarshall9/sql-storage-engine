// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Configurations;
internal sealed class PageFileConfiguration
{
    private string? path;
    public string Path { get => path!; set => path = value; }
    public int PageSize { get; set; }
    public bool ReadOnly { get; set; }
    public bool CreateNew { get; set; }
}