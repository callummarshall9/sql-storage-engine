// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Configurations;
internal sealed class MemoryPageConfiguration
{
    public int PageSize { get; set; }
    public bool ReservePageZero { get; set; }
}