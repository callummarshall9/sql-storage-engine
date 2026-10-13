// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Pages;
internal sealed record PageFileSession
{
    public Guid SessionId { get; init; }
    public string Path { get => path!; init => path = value; }

    private string? path;
    public int PageSize { get; init; }
    public bool ReadOnly { get; init; }
    public bool CreateNew { get; init; }
}