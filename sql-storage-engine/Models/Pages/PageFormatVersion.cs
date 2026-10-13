// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Pages;
public readonly record struct PageFormatVersion
{
    public ushort Value { get; init; }
}