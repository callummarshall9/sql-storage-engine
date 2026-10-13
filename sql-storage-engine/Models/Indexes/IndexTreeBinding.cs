// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Indexes;
public sealed record IndexTreeBinding
{
    internal IndexRootContext? RootContext { get; init; }
    internal object? Service { get; init; }
}