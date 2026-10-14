// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Pages;
/// <summary>Selects mutable writer ownership or non-modifying diagnostic access.</summary>
public enum DatabaseOpenMode
{
    Writer = 1,
    ReadOnly = 2
}