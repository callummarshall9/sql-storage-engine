// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Pages;
public sealed record PageDatabaseBinding
{
    internal object? Service { get; init; }
    internal PageDatabaseContext? Context { get; init; }
}