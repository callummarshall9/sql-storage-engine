// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Resources;
internal sealed class CapacityCounter
{
    public int Count;
    public int Maximum { get; init; }
}